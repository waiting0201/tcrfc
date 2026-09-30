using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>分眾條件（App 規劃書 §6.3）。三個維度，<b>刻意不做行為定向</b>：會籍層級、追蹤球隊、俱樂部歸屬；語系由裝置決定（推播文案依裝置語系送出）。</summary>
public sealed record PushAudienceSpec(string Tier, Guid? ClubId, IReadOnlyList<string> TeamCodes)
{
    public static readonly IReadOnlySet<string> Tiers = new HashSet<string>(["all", "fan_club", "registered", "anonymous"], StringComparer.Ordinal);

    public static IReadOnlyList<string> ParseTeamCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static PushAudienceSpec From(PushMessage m) => new(m.AudienceTier, m.AudienceClubId, ParseTeamCodes(m.AudienceTeamCodes));
}

/// <summary>
/// 把分眾條件解析成「裝置」查詢（docs/19 §5：分眾一律在 .NET 端解析成裝置清單，不使用 FCM topic，付費狀態不送進 Google 的索引）。
/// 一定要同時滿足：權杖有效、推播權限為已允許（或暫時允許）。追蹤球隊只計「推播開啟」（<c>is_push_enabled</c>）的訂閱。
/// </summary>
public static class PushAudience
{
    /// <summary>可實際送出推播的裝置：符合分眾條件、權杖有效、推播權限已允許。</summary>
    public static IQueryable<AppDevice> Devices(ClubDbContext db, PushAudienceSpec spec)
        => Filter(db, db.AppDevices.AsNoTracking()
            .Where(d => d.PushTokenStatus == "valid" && (d.PushPermission == "granted" || d.PushPermission == "provisional")), spec);

    /// <summary>只套分眾條件（不看權杖與推播權限）：用於「這台裝置是不是這則訊息／公告的對象」的判斷。</summary>
    public static IQueryable<AppDevice> Filter(ClubDbContext db, IQueryable<AppDevice> source, PushAudienceSpec spec)
    {
        var today = TaiwanClock.Today;
        var q = source;
        var clubId = spec.ClubId;

        switch (spec.Tier)
        {
            case "anonymous":
                q = q.Where(d => d.MemberId == null);
                break;
            case "fan_club":
                q = q.Where(d => d.MemberId != null && db.Memberships.Any(m =>
                    m.MemberId == d.MemberId && m.Tier == "fan_club" && m.Status == "active"
                    && (m.MembershipEndOn == null || m.MembershipEndOn >= today) && (clubId == null || m.ClubId == clubId)));
                break;
            case "registered":
                q = q.Where(d => d.MemberId != null && !db.Memberships.Any(m =>
                    m.MemberId == d.MemberId && m.Tier == "fan_club" && m.Status == "active"
                    && (m.MembershipEndOn == null || m.MembershipEndOn >= today) && (clubId == null || m.ClubId == clubId)));
                break;
        }

        if (clubId is not null)
        {
            // 俱樂部歸屬：追蹤該俱樂部、追蹤該俱樂部旗下的球隊、或持有該俱樂部的有效會籍。
            var clubCode = db.Clubs.Where(c => c.Id == clubId).Select(c => c.Code);
            var clubTeamCodes = db.Teams.Where(t => t.ClubId == clubId).Select(t => t.Code);
            q = q.Where(d =>
                d.PushTopicSubscriptions.Any(s => s.IsFollowing && ((s.TopicType == "club" && clubCode.Contains(s.TopicValue)) || (s.TopicType == "team" && clubTeamCodes.Contains(s.TopicValue))))
                || (d.MemberId != null && db.Memberships.Any(m => m.MemberId == d.MemberId && m.ClubId == clubId && m.Status == "active")));
        }

        if (spec.TeamCodes.Count > 0)
        {
            var codes = spec.TeamCodes.ToList();
            q = q.Where(d => d.PushTopicSubscriptions.Any(s => s.TopicType == "team" && s.IsPushEnabled && codes.Contains(s.TopicValue)));
        }

        return q;
    }
}
