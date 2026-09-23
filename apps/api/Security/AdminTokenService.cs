using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Tcrfc.Api.Security;

/// <summary>
/// 後台登入權杖的簽發與雜湊工具。純運算，不碰資料庫——資料庫寫入（<c>admin_refresh_tokens</c>
/// 落地、輪替、撤銷）由 <c>Features/AdminAuth/AdminAuthService</c> 負責，這裡只管密碼學。
///
/// ── 權杖設計（執行層決定，理由見 apps/api/README.md「後台登入權杖設計」整節）──
/// 1. **存取權杖（access token）是短效 JWT**（預設 15 分鐘），簽章金鑰讀 <c>JWT_SIGNING_KEY_CLUB</c>
///    ——這個鍵名已經在 deploy/dev/club.env／docs/20-cicd.md §7.2 預留，不是本次新發明。
///    Claims 只放身分（admin_user_id／username／is_super_admin／jti），**不放權限與俱樂部範圍**：
///    後者若隨 token 簽入，撤銷 AdminUserClub 授權或調整角色權限要等到 token 過期才生效，
///    與規劃書「到期自動失效」「資料範圍必須在資料存取層強制」直接衝突。每個受保護端點
///    仍會對 admin_user_clubs／role_permissions 即時查庫（見 AdminClubAuthorizer／
///    PermissionChecker），15 分鐘的短效期是「即使查詢層有快取空窗，最多暴露多久」的上限。
/// 2. **更新權杖（refresh token）是不透明亂數字串，不是 JWT**——比照 docs/19-app-tech-stack.md
///    對 App 更新權杖的既有硬性要求（「必須是可由伺服器端撤銷的不透明字串，不得用長效 JWT」），
///    後台採用同一套哲學是刻意的一致性選擇，不是巧合。伺服器只存 SHA-256 雜湊
///    （<c>admin_refresh_tokens.token_hash</c>），原始權杖只出現在回應當下一次。
/// </summary>
public sealed class AdminTokenService(IConfiguration configuration)
{
    public const string ConfigKey = "JWT_SIGNING_KEY_CLUB";
    public const string Issuer = "tcrfc-admin";
    public const string Audience = "tcrfc-admin-api";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    private SymmetricSecurityKey SigningKey
    {
        get
        {
            var key = configuration[ConfigKey];
            if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            {
                // ⛔ 正式環境的簽章金鑰太短等於整個系統的登入可以被暴力破解偽造——
                // 寧可啟動失敗，不要用一把弱金鑰悄悄跑起來。
                throw new InvalidOperationException(
                    $"{ConfigKey} 未設定或長度不足 32 字元，無法安全簽發後台登入權杖。");
            }
            return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
        }
    }

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(Guid adminUserId, string username, bool isSuperAdmin)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("username", username),
            new("is_super_admin", isSuperAdmin ? "true" : "false"),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        ClockSkew = TimeSpan.FromSeconds(30), // 短效 token 用小容差，不用預設的 5 分鐘
    };

    /// <summary>產生一組新的更新權杖原始值（呼叫端負責雜湊後落地，這裡不碰 DB）。
    /// 256 bits 亂數，Base64Url 編碼（不含 padding，適合放進 cookie／URL 不需額外跳脫）。</summary>
    public static string GenerateRefreshTokenValue()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>更新權杖只存雜湊，原始值只在核發當下出現一次。SHA-256 十六進位字串。</summary>
    public static string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
