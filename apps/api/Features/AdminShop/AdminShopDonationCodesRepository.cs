using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S6 電子發票的「捐贈碼名單」（規劃書 §4.13 S6）：結帳時顧客可選擇把發票捐贈給哪個機構。捐贈碼名單<b>全系統共用</b>（不帶 <c>club_id</c>），
/// 因此走全域端點（<see cref="Security.IAdminSystemAuthorizer"/>，權限 <c>shop.donation_code.*</c>，不分俱樂部）。
/// 捐贈碼是 3 到 7 位數字（財政部愛心碼）。已停用的碼不會出現在前台選單但保留紀錄。
/// </summary>
public sealed partial class AdminShopDonationCodesRepository(ClubDbContext db)
{
    [GeneratedRegex(@"^\d{3,7}$")]
    private static partial Regex CodeFormat();

    public async Task<IReadOnlyList<AdminDonationCodeDto>> ListAsync(CancellationToken cancellationToken)
        => (await db.InvoiceDonationCodes.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<AdminDonationCodeDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.InvoiceDonationCodes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<AdminDonationCodeDto> CreateAsync(UpsertAdminDonationCodeRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (code, org) = Validate(request);
        if (await db.InvoiceDonationCodes.AsNoTracking().AnyAsync(c => c.Code == code, cancellationToken))
        {
            throw new AdminConflictException("捐贈碼重複", $"捐贈碼「{code}」已經在名單裡了。", "code");
        }

        var maxOrder = await db.InvoiceDonationCodes.Select(c => (int?)c.SortOrder).MaxAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var row = new InvoiceDonationCode
        {
            Id = Guid.NewGuid(), Code = code, OrgName = org, IsActive = request.IsActive, SortOrder = request.SortOrder ?? (maxOrder ?? -1) + 1,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.InvoiceDonationCodes.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<AdminDonationCodeDto?> UpdateAsync(Guid id, UpsertAdminDonationCodeRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (code, org) = Validate(request);
        var row = await db.InvoiceDonationCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (await db.InvoiceDonationCodes.AsNoTracking().AnyAsync(c => c.Code == code && c.Id != id, cancellationToken))
        {
            throw new AdminConflictException("捐贈碼重複", $"捐贈碼「{code}」已經在名單裡了。", "code");
        }

        row.Code = code;
        row.OrgName = org;
        row.IsActive = request.IsActive;
        if (request.SortOrder is int order)
        {
            row.SortOrder = order;
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.InvoiceDonationCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        // 已開立的發票裡存的是捐贈碼本身（store_invoices.donation_code），不會因為名單刪除而失去依據。
        db.InvoiceDonationCodes.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Code, string Org) Validate(UpsertAdminDonationCodeRequest request)
    {
        var code = AdminInput.RequireText(request.Code, "捐贈碼", 16, "code");
        if (!CodeFormat().IsMatch(code))
        {
            throw new AdminValidationException("捐贈碼必須是 3 到 7 位數字。", "code");
        }

        AdminInput.OptionalNonNegative(request.SortOrder, "排序", "sortOrder");
        return (code, AdminInput.RequireText(request.OrgName, "機構名稱", 128, "orgName"));
    }

    private static AdminDonationCodeDto ToDto(InvoiceDonationCode c) => new()
    {
        Id = c.Id, Code = c.Code, OrgName = c.OrgName, IsActive = c.IsActive, SortOrder = c.SortOrder, UpdatedAt = c.UpdatedAt,
    };
}
