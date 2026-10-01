using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>已通過驗證的會員身分（本人）。只有 <see cref="MemberAuthenticator"/> 建得出來。</summary>
public sealed class MemberIdentity
{
    public Guid MemberId { get; }
    public string MemberNo { get; }
    public bool EmailVerified { get; }

    internal MemberIdentity(Guid memberId, string memberNo, bool emailVerified)
    {
        MemberId = memberId;
        MemberNo = memberNo;
        EmailVerified = emailVerified;
    }
}

/// <summary>
/// 會員端點的「這個人是誰」：驗證 <c>MemberBearer</c> 存取權杖，再<b>即時查庫</b>確認帳號仍是啟用狀態
/// （停用、已刪除、已被合併的帳號立刻失效，不必等 15 分鐘的權杖過期）。
/// 任何回傳個資的會員端點都只能拿這裡給的 <see cref="MemberIdentity.MemberId"/> 當查詢條件——
/// 「只能回傳呼叫者本人的資料」（App 規劃書 §9.3）靠的是「條件永遠來自權杖、不來自路由或請求本文」，不是靠記得檢查。
/// </summary>
public sealed class MemberAuthenticator(ClubDbContext db)
{
    public async Task<MemberIdentity> RequireAsync(HttpContext httpContext, CancellationToken cancellationToken, bool requireVerifiedEmail = false)
    {
        var identity = await TryAsync(httpContext, cancellationToken) ?? throw new MemberUnauthenticatedException();
        if (requireVerifiedEmail && !identity.EmailVerified)
        {
            throw new MemberForbiddenException("請先完成 Email 驗證後再使用這個功能。", "email_not_verified");
        }

        return identity;
    }

    /// <summary>沒帶權杖或權杖無效回 null（給「會員可選」的端點，如球迷會活動報名）；帶了有效權杖但帳號已停用也是 null。</summary>
    public async Task<MemberIdentity?> TryAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await httpContext.AuthenticateAsync(MemberTokenService.Scheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return null;
        }

        var sub = result.Principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? result.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var memberId))
        {
            return null;
        }

        var row = await db.Members.AsNoTracking().Where(m => m.Id == memberId)
            .Select(m => new { m.Id, m.MemberNo, m.Status, m.EmailVerifiedAt }).FirstOrDefaultAsync(cancellationToken);
        return row is null || row.Status != "active"
            ? null
            : new MemberIdentity(row.Id, row.MemberNo, row.EmailVerifiedAt is not null);
    }
}
