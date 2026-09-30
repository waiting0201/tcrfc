using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Proposals;

/// <summary>
/// 公開：E3 提案簡介下載（前台 9.4 CTA）。流程＝填表單（公司／姓名／Email／同意條款）→ 建立 Lead → 取得**限時**下載連結
/// → 憑連結由 API 串流檔案。提案檔放私有容器，沒有任何公開網址；連結權杖用 ASP.NET Core Data Protection 的
/// time-limited protector 簽發（30 分鐘，與 Lead 記錄無關，不查庫即可驗證），綁定俱樂部與檔案，換俱樂部路徑或過期一律失效。
/// Lead 建立重用 <see cref="FormsRepository.SubmitAsync"/>（表單 <c>proposal_download</c> 的欄位驗證與寫入單一來源），
/// 並帶上 <c>proposal_id</c>（A/B 版本追蹤）。
/// </summary>
public sealed partial class ProposalsRepository(
    IClubSqlConnectionFactory connectionFactory, IQueryCache cache, FormsRepository forms, IDataProtectionProvider protectionProvider)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private const string ProtectorPurpose = "Tcrfc.Proposals.Download.v1";

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    private sealed record ProposalRow(Guid Id, string Title, int VersionNo);
    private sealed record FileRow(Guid Id, Guid ProposalId, string Locale, string FileKey, int VersionNo);

    public async Task<IReadOnlyList<PublicProposalDto>> ListAsync(ClubScope scope, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync("proposals", scope.ClubCode, CacheDimensions.AnyLocale, "list", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var proposals = (await connection.QueryAsync<ProposalRow>(new CommandDefinition(
                "SELECT id AS Id, title AS Title, version_no AS VersionNo FROM proposals WHERE club_id = @ClubId AND status = 'published' ORDER BY row_seq",
                new { scope.ClubId }, cancellationToken: ct))).AsList();
            if (proposals.Count == 0)
            {
                return (IReadOnlyList<PublicProposalDto>)[];
            }

            var files = await connection.QueryAsync<(Guid ProposalId, string Locale)>(new CommandDefinition(
                "SELECT proposal_id AS ProposalId, locale AS Locale FROM proposal_files WHERE proposal_id IN @Ids",
                new { Ids = proposals.Select(p => p.Id).ToList() }, cancellationToken: ct));
            var byProposal = files.GroupBy(f => f.ProposalId).ToDictionary(g => g.Key, g => g.Select(f => f.Locale == "en" ? "en" : "zh").Distinct().OrderBy(l => l).ToList());
            return (IReadOnlyList<PublicProposalDto>)proposals.Where(p => byProposal.ContainsKey(p.Id))
                .Select(p => new PublicProposalDto { Id = p.Id, Title = p.Title, VersionNo = p.VersionNo, Locales = byProposal[p.Id] }).ToList();
        }, cancellationToken);

    /// <summary>建立 Lead 並簽發下載連結；提案不存在／未發布／沒有檔案回傳 <c>null</c>（呼叫端轉 404）。</summary>
    public async Task<ProposalDownloadResultDto?> RequestDownloadAsync(
        ClubScope scope, Guid proposalId, ProposalDownloadRequest request, CancellationToken cancellationToken)
    {
        // 誘捕欄位：機器人。安靜回成功，不寫入、不給連結。
        if (!string.IsNullOrEmpty(request.Website))
        {
            return new ProposalDownloadResultDto();
        }

        var company = Require(request.Company, "公司名稱", 128);
        var name = Require(request.Name, "姓名", 64);
        var email = Require(request.Email, "Email", 255);
        if (!EmailFormat().IsMatch(email))
        {
            throw new PublicFormSubmissionValidationException("Email 格式不正確。");
        }

        if (!request.Consent)
        {
            throw new PublicFormSubmissionValidationException("請勾選同意條款後才能取得下載連結。");
        }

        var file = await PickFileAsync(scope, proposalId, RequestLocale.ToDbLocale(request.Lang), cancellationToken);
        if (file is null)
        {
            return null;
        }

        await forms.SubmitAsync(scope, FormCatalog.ProposalDownload, new SubmitFormRequest
        {
            Answers = new Dictionary<string, string>
            {
                ["company"] = company, ["name"] = name, ["contact"] = email, ["privacy_consent"] = "true",
            },
            SourcePath = request.SourcePath, UtmSource = request.UtmSource, UtmCampaign = request.UtmCampaign,
        }, cancellationToken, proposalId);

        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var token = Protector().Protect($"{scope.ClubId:N}:{file.Id:N}", expiresAt);
        return new ProposalDownloadResultDto { DownloadPath = $"/api/v1/{scope.ClubCode}/proposals/downloads/{token}", ExpiresAt = expiresAt.UtcDateTime };
    }

    /// <summary>驗證下載權杖並回傳檔案物件鍵；權杖無效／過期／換俱樂部／檔案已被刪除或提案已改回草稿皆回傳 <c>null</c>。</summary>
    public async Task<string?> ResolveDownloadAsync(ClubScope scope, string token, CancellationToken cancellationToken)
    {
        string payload;
        try
        {
            payload = Protector().Unprotect(token, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var parts = payload.Split(':');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var clubId) || clubId != scope.ClubId
            || !Guid.TryParseExact(parts[1], "N", out var fileId))
        {
            return null;
        }

        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition("""
            SELECT f.file_key FROM proposal_files f JOIN proposals p ON p.id = f.proposal_id
            WHERE f.id = @FileId AND p.club_id = @ClubId AND p.status = 'published'
            """, new { FileId = fileId, scope.ClubId }, cancellationToken: cancellationToken));
    }

    /// <summary>挑檔：優先請求語系、否則回退中文；同語系取版本號最大的。</summary>
    private async Task<FileRow?> PickFileAsync(ClubScope scope, Guid proposalId, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var files = (await connection.QueryAsync<FileRow>(new CommandDefinition("""
            SELECT f.id AS Id, f.proposal_id AS ProposalId, f.locale AS Locale, f.file_key AS FileKey, f.version_no AS VersionNo
            FROM proposal_files f JOIN proposals p ON p.id = f.proposal_id
            WHERE p.id = @ProposalId AND p.club_id = @ClubId AND p.status = 'published'
            """, new { ProposalId = proposalId, scope.ClubId }, cancellationToken: cancellationToken))).AsList();
        return files.Where(f => f.Locale == dbLocale).OrderByDescending(f => f.VersionNo).FirstOrDefault()
            ?? files.Where(f => f.Locale == RequestLocale.DefaultDbLocale).OrderByDescending(f => f.VersionNo).FirstOrDefault()
            ?? files.OrderByDescending(f => f.VersionNo).FirstOrDefault();
    }

    private ITimeLimitedDataProtector Protector() => protectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();

    private static string Require(string? value, string label, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new PublicFormSubmissionValidationException($"請填寫{label}。");
        }

        return trimmed.Length > maxLength
            ? throw new PublicFormSubmissionValidationException($"{label}不可超過 {maxLength} 個字。")
            : trimmed;
    }
}
