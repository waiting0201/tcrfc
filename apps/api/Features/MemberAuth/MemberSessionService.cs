using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberAuth;

public sealed record MemberSessionTokens(
    string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc, bool IsPersistent);

/// <summary>
/// 會員（網頁）更新權杖的核發、輪替、撤銷（<c>member_refresh_tokens</c>）。機制與 <c>AdminAuthService</c> 一致：
/// 不透明亂數、只存 SHA-256 雜湊、每次使用即輪替、<b>已撤銷的權杖再被使用＝外洩，撤銷該會員全部有效權杖</b>（App 規劃書 §4.3 的同一套哲學）。
/// 「記住我」：存續 30 天（Cookie 帶到期日）；否則 24 小時（工作階段 Cookie）。輪替時沿用同一種類型，並重新計算效期（滑動）。
/// </summary>
public sealed class MemberSessionService(ClubDbContext db, MemberTokenService tokens)
{
    public static readonly TimeSpan PersistentLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    public async Task<MemberSessionTokens> IssueAsync(Guid memberId, bool persistent, CancellationToken cancellationToken)
    {
        var (row, raw) = NewRow(memberId, persistent);
        db.MemberRefreshTokens.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToTokens(memberId, raw, row);
    }

    /// <summary>用更新權杖換一組新的。回 null＝無效（不存在／過期／帳號不再啟用／被重放）；呼叫端一律回 401，不分原因。</summary>
    public async Task<(MemberSessionTokens Tokens, Guid MemberId)?> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 256)
        {
            return null;
        }

        var hash = AdminTokenService.HashRefreshToken(rawToken);
        var row = await db.MemberRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (row.RevokedAt is not null)
        {
            // 重放偵測：這把權杖早就被輪替掉了，還有人拿著它來用——合法使用者手上已經有新的，所以這一定是外流。整批撤銷。
            await RevokeAllAsync(row.MemberId, cancellationToken);
            return null;
        }

        if (row.ExpiresAt <= now)
        {
            return null;
        }

        var active = await db.Members.AsNoTracking().AnyAsync(m => m.Id == row.MemberId && m.Status == "active", cancellationToken);
        if (!active)
        {
            return null;
        }

        var (next, raw) = NewRow(row.MemberId, row.IsPersistent);
        row.RevokedAt = now;
        row.ReplacedBy = next;
        db.MemberRefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken);
        return (ToTokens(row.MemberId, raw, next), row.MemberId);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 256)
        {
            return;
        }

        var hash = AdminTokenService.HashRefreshToken(rawToken);
        var now = DateTime.UtcNow;
        await db.MemberRefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    /// <summary>撤銷該會員名下全部尚未撤銷的更新權杖（登出全部裝置、變更／重設密碼、重放偵測、刪除帳號）。</summary>
    public async Task RevokeAllAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.MemberRefreshTokens.Where(t => t.MemberId == memberId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private (MemberRefreshToken Row, string Raw) NewRow(Guid memberId, bool persistent)
    {
        var raw = AdminTokenService.GenerateRefreshTokenValue();
        var now = DateTime.UtcNow;
        return (new MemberRefreshToken
        {
            Id = Guid.NewGuid(), MemberId = memberId, TokenHash = AdminTokenService.HashRefreshToken(raw), IsPersistent = persistent,
            IssuedAt = now, ExpiresAt = now.Add(persistent ? PersistentLifetime : SessionLifetime),
        }, raw);
    }

    private MemberSessionTokens ToTokens(Guid memberId, string raw, MemberRefreshToken row)
    {
        var (access, accessExpires) = tokens.IssueAccessToken(memberId);
        return new MemberSessionTokens(access, accessExpires, raw, row.ExpiresAt, row.IsPersistent);
    }
}

/// <summary>
/// 會員註冊／入會時「確保有一份免費（一般會員）會籍與一張會員卡」（規劃書 §3.14：免費註冊＋Email 驗證即為一般會員，有電子會員卡）。
/// 會籍是「一人每俱樂部每球季一份」：目前球季＝涵蓋今天的球季，沒有就取最近的未來球季，兩者都沒有回 null（俱樂部尚未建立球季）。
/// </summary>
public sealed class MemberMembershipService(ClubDbContext db)
{
    public async Task<Season?> CurrentSeasonAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        return await db.Seasons.AsNoTracking().Where(s => s.ClubId == clubId && s.EndOn >= today)
            .OrderBy(s => s.StartOn).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Membership?> EnsureRegisteredAsync(Guid memberId, string memberName, Guid clubId, CancellationToken cancellationToken)
    {
        var season = await CurrentSeasonAsync(clubId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var existing = await db.Memberships.FirstOrDefaultAsync(
            m => m.MemberId == memberId && m.ClubId == clubId && m.SeasonId == season.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        var membership = new Membership
        {
            Id = Guid.NewGuid(), MemberId = memberId, ClubId = clubId, SeasonId = season.Id, Tier = "registered", Status = "active",
            MembershipStartOn = season.StartOn, MembershipEndOn = season.EndOn, CreatedAt = now, UpdatedAt = now,
        };
        var card = new MemberCard
        {
            Id = Guid.NewGuid(), MembershipId = membership.Id, ClubId = clubId, HolderName = memberName, Token = SecureToken.Generate(),
            Status = "active", IssuedAt = now, CreatedAt = now, UpdatedAt = now,
        };
        db.Memberships.Add(membership);
        db.MemberCards.Add(card);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return membership;
        }
        catch (DbUpdateException)
        {
            // 並行請求搶先建立了同一份會籍（唯一鍵 member×club×season）：丟掉我們這份、改讀現有的。
            db.Entry(card).State = EntityState.Detached;
            db.Entry(membership).State = EntityState.Detached;
            return await db.Memberships.FirstOrDefaultAsync(
                m => m.MemberId == memberId && m.ClubId == clubId && m.SeasonId == season.Id, cancellationToken);
        }
    }
}
