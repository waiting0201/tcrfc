using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberAuth;

public sealed record AppDeviceSummary(
    Guid DeviceId, string Platform, string? OsVersion, string? AppVersion, DateTime LastActiveAt, bool HasActiveSession);

/// <summary>
/// App 登入的更新權杖鏈（AP-3，2026-10-02；App 規劃書 §4.3／§4.4／§10.1、docs/19 §4）。<b>鏈掛在 <c>app_devices</c> 上</b>
/// （一裝置一鏈，與「一個裝置同時只綁一個會員」天然 1:1），不另建表；網頁登入仍走 <c>member_refresh_tokens</c>（見 <see cref="MemberSessionService"/>）。
/// 規則：
/// ① <b>發放</b>：會員登入時帶 <c>deviceInstallId</c>（裝置須已註冊），核發 90 天滑動的不透明權杖（格式與簽章見 <see cref="AppRefreshTokenCodec"/>），
///    只存 SHA-256 雜湊，並把 <c>member_id</c> 綁到該裝置（舊的綁定直接被取代）。
/// ② <b>輪替</b>：每次使用即換新（新雜湊、新到期、新簽發時間）；一條鏈同一時間只有一把有效權杖。
/// ③ <b>重用偵測</b>：簽章合法（確實是我們核發過的）但不是現行那把、且簽發時間早於上次輪替 → 外洩 → 撤銷整條鏈（雜湊清空、<c>revoked_at</c> 設值、解除會員綁定）。
///    簽章不合法的字串一律無副作用地拒絕，所以亂送權杖不會登出任何人。
///    ⚠️ 取捨：行動網路下「伺服器已輪替、回應卻在途中遺失」，用戶端會拿舊權杖重試，這會被判為重用而登出該裝置，需重新登入。
///    規劃書 §10.1 只給四個欄位、沒有「前一把雜湊」，無法安全地容忍這種重試；寧可重新登入，不放寬重用偵測。
/// ④ <b>撤銷</b>：登出（單一裝置）、登出全部裝置／改密碼／重設密碼／刪除帳號（<see cref="RevokeAllForMemberAsync"/>）、會員自行撤銷某裝置、重用偵測。
///    撤銷後裝置紀錄保留（推播訂閱仍在），只是解除會員綁定。
/// 存取權杖是 15 分鐘的 JWT（無狀態），撤銷後最多再有效 15 分鐘——與網頁會員工作階段同一個取捨（docs/19 §4）。
/// </summary>
public sealed class AppDeviceSessionService(ClubDbContext db, MemberTokenService tokens, IConfiguration configuration, ILogger<AppDeviceSessionService> logger)
{
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);

    private readonly byte[] key = AppRefreshTokenCodec.DeriveKey(configuration);

    public async Task<MemberSessionTokens> IssueAsync(string deviceInstallId, Guid memberId, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(deviceInstallId);
        var device = await db.AppDevices.AsNoTracking().Where(d => d.DeviceInstallId == deviceInstallId)
            .Select(d => new { d.Id, d.RefreshTokenRotatedAt }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new MemberValidationException("這支裝置尚未註冊，請重新開啟 App 後再試。", "device_not_registered");

        var now = DateTime.UtcNow;
        var stampMs = NextStampMs(now, device.RefreshTokenRotatedAt);
        var raw = AppRefreshTokenCodec.Create(key, device.Id, stampMs);
        var expires = now.Add(RefreshLifetime);
        var hash = AppRefreshTokenCodec.Hash(raw);
        var stamp = AppRefreshTokenCodec.ToUtc(stampMs);

        await db.AppDevices.Where(d => d.Id == device.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.MemberId, memberId)
            .SetProperty(d => d.RefreshTokenHash, hash)
            .SetProperty(d => d.RefreshTokenExpiresAt, expires)
            .SetProperty(d => d.RefreshTokenRotatedAt, stamp)
            .SetProperty(d => d.RevokedAt, (DateTime?)null)
            .SetProperty(d => d.LastActiveAt, now), cancellationToken);
        await db.PushTopicSubscriptions.Where(p => p.DeviceId == device.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.MemberId, memberId), cancellationToken);
        return ToTokens(memberId, raw, expires);
    }

    /// <summary>回 null＝無效（格式或簽章不對、查無裝置、鏈已撤銷、過期、會員不再啟用、重用）；呼叫端一律回 401，不分原因。</summary>
    public async Task<(MemberSessionTokens Tokens, Guid MemberId)?> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var parsed = AppRefreshTokenCodec.TryParse(key, rawToken);
        if (parsed is null)
        {
            return null;
        }

        var device = await db.AppDevices.AsNoTracking().Where(d => d.Id == parsed.DeviceId)
            .Select(d => new { d.Id, d.MemberId, d.RefreshTokenHash, d.RefreshTokenExpiresAt, d.RefreshTokenRotatedAt, d.RevokedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (device is null || device.RevokedAt is not null || device.RefreshTokenHash is null || device.MemberId is null)
        {
            return null; // 鏈已撤銷或根本沒有鏈：不是重用（撤銷後的舊權杖被送來不再擴大處置）
        }

        var hash = AppRefreshTokenCodec.Hash(rawToken);
        if (!string.Equals(hash, device.RefreshTokenHash, StringComparison.Ordinal))
        {
            var currentStampMs = device.RefreshTokenRotatedAt is null ? long.MaxValue : AppRefreshTokenCodec.ToStampMs(device.RefreshTokenRotatedAt.Value);
            if (parsed.StampMs < currentStampMs)
            {
                // 簽章合法＝確實核發過；簽發時間早於現行那把＝已被輪替掉，卻有人拿著它來用 → 外洩。撤銷整條鏈。
                logger.LogWarning("偵測到 App 更新權杖重用，已撤銷裝置 {DeviceId} 的整條鏈。", device.Id);
                await RevokeChainAsync(device.Id, cancellationToken);
            }

            return null;
        }

        var now = DateTime.UtcNow;
        if (device.RefreshTokenExpiresAt is null || device.RefreshTokenExpiresAt <= now)
        {
            return null;
        }

        var memberId = device.MemberId.Value;
        var active = await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId && m.Status == "active", cancellationToken);
        if (!active)
        {
            await RevokeChainAsync(device.Id, cancellationToken);
            return null;
        }

        var stampMs = NextStampMs(now, device.RefreshTokenRotatedAt);
        var raw = AppRefreshTokenCodec.Create(key, device.Id, stampMs);
        var expires = now.Add(RefreshLifetime);
        var newHash = AppRefreshTokenCodec.Hash(raw);
        var stamp = AppRefreshTokenCodec.ToUtc(stampMs);

        // 條件式更新（比對舊雜湊且未撤銷）：兩個請求用同一把權杖並行輪替時，只有一個成功。
        var affected = await db.AppDevices.Where(d => d.Id == device.Id && d.RefreshTokenHash == hash && d.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.RefreshTokenHash, newHash)
                .SetProperty(d => d.RefreshTokenExpiresAt, expires)
                .SetProperty(d => d.RefreshTokenRotatedAt, stamp)
                .SetProperty(d => d.LastActiveAt, now), cancellationToken);
        if (affected == 0)
        {
            // 輸了並行競爭：對方已輪替，我手上這把等同已被用過 → 與重用同樣處置。
            logger.LogWarning("App 更新權杖並行輪替衝突，已撤銷裝置 {DeviceId} 的整條鏈。", device.Id);
            await RevokeChainAsync(device.Id, cancellationToken);
            return null;
        }

        return (ToTokens(memberId, raw, expires), memberId);
    }

    /// <summary>登出這支裝置。只有「現行那把」權杖才撤銷（過期或已輪替掉的舊權杖不處置，避免被拿來登出別人）。</summary>
    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var parsed = AppRefreshTokenCodec.TryParse(key, rawToken);
        if (parsed is null)
        {
            return;
        }

        var hash = AppRefreshTokenCodec.Hash(rawToken);
        var deviceId = await db.AppDevices.AsNoTracking().Where(d => d.Id == parsed.DeviceId && d.RefreshTokenHash == hash)
            .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(cancellationToken);
        if (deviceId is not null)
        {
            await RevokeChainAsync(deviceId.Value, cancellationToken);
        }
    }

    /// <summary>撤銷該會員名下所有裝置的鏈並解除綁定（登出全部裝置、變更／重設密碼、刪除帳號）。</summary>
    public async Task RevokeAllForMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.AppDevices.Where(d => d.MemberId == memberId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.RefreshTokenHash, (string?)null)
            .SetProperty(d => d.RefreshTokenExpiresAt, (DateTime?)null)
            .SetProperty(d => d.RevokedAt, now)
            .SetProperty(d => d.MemberId, (Guid?)null), cancellationToken);
        await db.PushTopicSubscriptions.Where(p => p.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.MemberId, (Guid?)null), cancellationToken);
    }

    /// <summary>會員自己的裝置清單（不含 <c>device_install_id</c>、推播權杖等識別資料）。</summary>
    public async Task<IReadOnlyList<AppDeviceSummary>> ListForMemberAsync(Guid memberId, CancellationToken cancellationToken)
        => await db.AppDevices.AsNoTracking().Where(d => d.MemberId == memberId).OrderByDescending(d => d.LastActiveAt)
            .Select(d => new AppDeviceSummary(d.Id, d.Platform, d.OsVersion, d.AppVersion, d.LastActiveAt, d.RefreshTokenHash != null && d.RevokedAt == null))
            .ToListAsync(cancellationToken);

    /// <summary>會員撤銷自己名下的某一台裝置。不是自己的裝置一律當不存在（404），不洩漏存在與否。</summary>
    public async Task<bool> RevokeDeviceAsync(Guid memberId, Guid deviceId, CancellationToken cancellationToken)
    {
        var owns = await db.AppDevices.AsNoTracking().AnyAsync(d => d.Id == deviceId && d.MemberId == memberId, cancellationToken);
        if (!owns)
        {
            return false;
        }

        await RevokeChainAsync(deviceId, cancellationToken);
        return true;
    }

    private async Task RevokeChainAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.AppDevices.Where(d => d.Id == deviceId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.RefreshTokenHash, (string?)null)
            .SetProperty(d => d.RefreshTokenExpiresAt, (DateTime?)null)
            .SetProperty(d => d.RevokedAt, now)
            .SetProperty(d => d.MemberId, (Guid?)null), cancellationToken);
        await db.PushTopicSubscriptions.Where(p => p.DeviceId == deviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.MemberId, (Guid?)null), cancellationToken);
    }

    /// <summary>新簽發時間必須嚴格大於上一把（毫秒精度，與 datetime2(3) 一致），否則重用偵測的「早於」比較會失準。</summary>
    private static long NextStampMs(DateTime now, DateTime? previousRotatedAt)
    {
        var nowMs = AppRefreshTokenCodec.ToStampMs(now);
        return previousRotatedAt is null ? nowMs : Math.Max(nowMs, AppRefreshTokenCodec.ToStampMs(previousRotatedAt.Value) + 1);
    }

    private MemberSessionTokens ToTokens(Guid memberId, string raw, DateTime expires)
    {
        var (access, accessExpires) = tokens.IssueAccessToken(memberId);
        return new MemberSessionTokens(access, accessExpires, raw, expires, IsPersistent: true);
    }
}
