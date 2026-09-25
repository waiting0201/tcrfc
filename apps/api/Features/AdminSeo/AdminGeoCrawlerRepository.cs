using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// `GEO-02` AI 爬蟲授權（S1-12b）的讀寫。**不新增資料表**，兩個鍵存在 <c>settings</c>
/// （<c>setting_group='geo'</c>，跟 <c>llms.txt</c> 內容同一群組，都是 `GEO` 系列功能）：
/// - <c>geo.crawler_agents</c>：JSON 陣列 <c>[{"userAgent":"GPTBot","allowed":true}, ...]</c>。
///   **技術資料非人類語言，不進 i18n 側表**，跟 <c>seo.robots_custom_rules</c> 同一個判斷。
/// - <c>geo.crawler_extra_exclude_paths</c>：JSON 陣列 <c>["/zh/foo/", ...]</c>，後台**自行再加**
///   的排除路徑（規劃書「排除路徑清單」的可增減部分）。
///
/// 🔴 **強制排除路徑（<see cref="GeoCrawlerDefaults.GetMandatoryExcludePaths"/>）完全不經過這個
/// repository**——<see cref="GetAsync"/> 只是把它跟後台自己加的路徑一起附在回應裡讓畫面顯示
/// （唯讀），<see cref="UpdateAsync"/> 的請求型別（<see cref="UpdateCrawlerSettingsRequest"/>）
/// 根本沒有欄位可以承載「要移除哪個強制路徑」——結構上就不存在這個操作，不是靠這裡的程式碼判斷
/// 擋下來的，見 docs/14-invariants.md「這條排除是個資防線，不得為了『讓 AI 多抓一點』而放寬」。
/// 真正輸出給 <c>apps/web</c> 的合併結果（強制 ∪ 後台自加）在
/// <see cref="Seo.SeoRepository.GetCrawlerSettingsAsync"/>，不是這裡。
/// </summary>
public sealed class AdminGeoCrawlerRepository(ClubDbContext dbContext)
{
    private const string KeyAgents = "geo.crawler_agents";
    private const string KeyExtraExcludePaths = "geo.crawler_extra_exclude_paths";
    private const string Group = "geo";

    private const int MaxUserAgentLength = 100;
    private const int MaxAgentCount = 50;
    private const int MaxPathLength = 200;
    private const int MaxPathCount = 100;

    private static readonly Regex UserAgentPattern = new("^[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);
    private static readonly string[] AllKeys = [KeyAgents, KeyExtraExcludePaths];
    private static readonly JsonSerializerOptions StorageJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminCrawlerSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToListAsync(cancellationToken);

        return BuildDto(scope.ClubCode, settings);
    }

    /// <summary>整份取代語意。逐項驗證使用者代理字串格式與排除路徑格式，任一筆有誤整批不寫入
    /// （比照既有匯入類端點「整批驗證、任一列有誤整批不寫入」的一貫語意，這裡雖然不是 CSV 匯入，
    /// 但同樣是「使用者一次貼一整份清單過來」的表單，維持同一種使用者預期）。</summary>
    public async Task<AdminCrawlerSettingsDto> UpdateAsync(
        AdminClubScope scope, UpdateCrawlerSettingsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateUserAgents(request.UserAgents);
        ValidateExcludePaths(request.AdditionalExcludePaths);

        var settings = await dbContext.Settings
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        var agentsJson = JsonSerializer.Serialize(request.UserAgents, StorageJsonOptions);
        var pathsJson = JsonSerializer.Serialize(request.AdditionalExcludePaths, StorageJsonOptions);

        UpsertValue(settings, KeyAgents, scope.ClubId, agentsJson, operatorId);
        UpsertValue(settings, KeyExtraExcludePaths, scope.ClubId, pathsJson, operatorId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(scope, cancellationToken);
    }

    private static void ValidateUserAgents(IReadOnlyList<CrawlerAgentDto> agents)
    {
        if (agents.Count > MaxAgentCount)
        {
            throw new AdminSeoValidationException($"AI 使用者代理清單最多 {MaxAgentCount} 筆。");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            if (string.IsNullOrWhiteSpace(agent.UserAgent) || !UserAgentPattern.IsMatch(agent.UserAgent))
            {
                throw new AdminSeoValidationException(
                    $"使用者代理「{agent.UserAgent}」格式不正確，只能包含英數字、句點、連字號或底線，長度 1 至 {MaxUserAgentLength} 字元。");
            }

            if (!seen.Add(agent.UserAgent))
            {
                throw new AdminSeoValidationException($"使用者代理「{agent.UserAgent}」重複，同一份清單不能有兩筆相同的代理名稱。");
            }
        }
    }

    private static void ValidateExcludePaths(IReadOnlyList<string> paths)
    {
        if (paths.Count > MaxPathCount)
        {
            throw new AdminSeoValidationException($"排除路徑最多 {MaxPathCount} 筆。");
        }

        foreach (var path in paths)
        {
            RedirectPathPolicy.Validate(path, "排除路徑");

            if (path.Length > MaxPathLength)
            {
                throw new AdminSeoValidationException($"排除路徑「{path}」長度不能超過 {MaxPathLength} 字元。");
            }

            if (!path.EndsWith('/'))
            {
                throw new AdminSeoValidationException($"排除路徑「{path}」必須以「/」結尾（僅接受目錄前綴，不接受單一檔案路徑）。");
            }
        }
    }

    private AdminCrawlerSettingsDto BuildDto(string clubCode, IReadOnlyCollection<Setting> settings)
    {
        var agentsValue = settings.FirstOrDefault(s => s.SettingKey == KeyAgents)?.SettingValue;
        var pathsValue = settings.FirstOrDefault(s => s.SettingKey == KeyExtraExcludePaths)?.SettingValue;

        List<CrawlerAgentDto> agents = string.IsNullOrWhiteSpace(agentsValue)
            ? GeoCrawlerDefaults.DefaultUserAgents.Select(d => new CrawlerAgentDto { UserAgent = d.UserAgent, Allowed = d.Allowed }).ToList()
            : (JsonSerializer.Deserialize<List<CrawlerAgentDto>>(agentsValue, StorageJsonOptions) ?? new List<CrawlerAgentDto>());

        List<string> additionalPaths = string.IsNullOrWhiteSpace(pathsValue)
            ? new List<string>()
            : (JsonSerializer.Deserialize<List<string>>(pathsValue, StorageJsonOptions) ?? new List<string>());

        return new AdminCrawlerSettingsDto
        {
            UserAgents = agents,
            AdditionalExcludePaths = additionalPaths,
            MandatoryExcludePaths = GeoCrawlerDefaults.GetMandatoryExcludePaths(clubCode),
        };
    }

    private Setting GetOrCreate(Dictionary<string, Setting> settings, string key, Guid clubId, Guid? operatorId)
    {
        if (settings.TryGetValue(key, out var existing))
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = operatorId;
            return existing;
        }

        var now = DateTime.UtcNow;
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            SettingKey = key,
            SettingGroup = Group,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Settings.Add(setting);
        settings[key] = setting;
        return setting;
    }

    private void UpsertValue(Dictionary<string, Setting> settings, string key, Guid clubId, string value, Guid? operatorId)
    {
        var setting = GetOrCreate(settings, key, clubId, operatorId);
        setting.SettingValue = value;
    }
}
