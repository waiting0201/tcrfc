using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 慈善後台登入權杖的簽發與驗證參數。純運算，不碰資料庫（資料庫寫入由
/// <c>CharityAdminAuthService</c> 負責）。設計與 <see cref="AdminTokenService"/> 逐點對應，
/// 差別只在<b>三個必須與主站分開的值</b>：
///
/// 1. <b>簽章金鑰</b>讀 <c>JWT_SIGNING_KEY_CHARITY</c>（不是 <c>JWT_SIGNING_KEY_CLUB</c>）——
///    docs/17 §5、docs/20 §7.2：慈善後台是獨立帳號體系，金鑰不得與俱樂部共用。
/// 2. <b>issuer／audience</b> 不同（<see cref="Issuer"/>／<see cref="Audience"/>）——即使兩把金鑰
///    在某個環境被誤設成同一個值，主站的權杖也不會被慈善後台接受，反之亦然。
/// 3. <b>驗證方案名稱</b>（<see cref="SchemeName"/>）是第二個具名 JwtBearer 方案，不是預設方案：
///    主站端點讀的是預設方案認出的 <c>HttpContext.User</c>，慈善端點一律用
///    <c>HttpContext.AuthenticateAsync(SchemeName)</c> 明確驗證，兩邊互不可見對方的權杖。
///
/// 存取權杖 15 分鐘（claims 只放身分，不放權限，理由同主站：權限變更要即時生效）；更新權杖是
/// 不透明亂數字串、伺服器只存 SHA-256 雜湊，雜湊與產生函式直接共用 <see cref="AdminTokenService"/>
/// 的純靜態方法（沒有任何主站狀態）。
/// </summary>
public sealed class CharityTokenService(IConfiguration configuration)
{
    public const string ConfigKey = "JWT_SIGNING_KEY_CHARITY";
    public const string SchemeName = "CharityAdminBearer";
    public const string Issuer = "tcrfc-charity-admin";
    public const string Audience = "tcrfc-charity-admin-api";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    public const int MinSigningKeyLength = 32;

    /// <summary>啟動期驗證簽章金鑰（比照 E-79）：缺值或太短就讓行程啟動失敗，不要起得來卻每支端點都 500。</summary>
    public static void ValidateSigningKeyConfigured(IConfiguration configuration)
    {
        var key = configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"{ConfigKey} 未設定或長度不足 {MinSigningKeyLength} 字元，無法安全簽發慈善後台登入權杖。");
        }
    }

    private SymmetricSecurityKey SigningKey
    {
        get
        {
            ValidateSigningKeyConfigured(configuration);
            return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(configuration[ConfigKey]!));
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
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
