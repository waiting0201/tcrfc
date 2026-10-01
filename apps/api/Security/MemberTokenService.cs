using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Tcrfc.Api.Security;

/// <summary>
/// 會員（網頁與 App 共用的前台帳號）登入存取權杖的簽發與驗證參數（E 批，2026-10-01，S2-11）。
///
/// 🔴 <b>會員權杖與後台權杖必須分開，不可共用簽章用途</b>，這裡用三層隔離，缺一層另外兩層仍擋得住：
/// ① <b>獨立的驗證機制名稱</b>（<see cref="Scheme"/>＝<c>MemberBearer</c>）：後台端點讀預設機制、會員端點只讀這個機制，互不認帳；
/// ② <b>不同的 issuer／audience</b>（<c>tcrfc-member</c>／<c>tcrfc-member-api</c>，後台是 <c>tcrfc-admin</c>／<c>tcrfc-admin-api</c>）：拿後台權杖打會員端點、反之亦然，驗證都會因 audience 不符而失敗；
/// ③ <b>不同的簽章金鑰</b>：優先讀 <c>JWT_SIGNING_KEY_MEMBER</c>（正式環境應另外設定一把獨立的）；<b>未設定時</b>由 <c>JWT_SIGNING_KEY_CLUB</c>
/// 以 HKDF-SHA256 加上用途標籤 <c>tcrfc-member-access-token-v1</c> 衍生——衍生出的金鑰與原金鑰在密碼學上互相獨立（拿到其中一把推不出另一把），
/// 所以「忘了設定獨立金鑰」不會退化成「兩邊共用同一把」，本機開發與既有測試也不必多設一個環境變數就能啟動。
///
/// 存取權杖 15 分鐘，claims 只放 <c>sub</c>（會員 id）與 <c>jti</c>，<b>不放姓名、Email、會籍或權限</b>——
/// 每個受保護端點仍即時查庫確認帳號狀態（<see cref="MemberAuthenticator"/>），停用帳號不必等權杖過期。
/// 更新權杖是不透明亂數、只存雜湊，比照後台（<see cref="AdminTokenService.GenerateRefreshTokenValue"/>／<see cref="AdminTokenService.HashRefreshToken"/>）。
/// </summary>
public sealed class MemberTokenService(IConfiguration configuration)
{
    public const string Scheme = "MemberBearer";
    public const string ConfigKey = "JWT_SIGNING_KEY_MEMBER";
    public const string Issuer = "tcrfc-member";
    public const string Audience = "tcrfc-member-api";
    private const string DerivationLabel = "tcrfc-member-access-token-v1";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>啟動期驗證：兩把金鑰至少有一把可用且夠長，否則讓行程啟動失敗（同 E-79 的做法）。</summary>
    public static void ValidateConfigured(IConfiguration configuration) => _ = DeriveKeyBytes(configuration);

    public static byte[] DeriveKeyBytes(IConfiguration configuration)
    {
        var dedicated = configuration[ConfigKey];
        if (!string.IsNullOrWhiteSpace(dedicated))
        {
            if (dedicated.Length < AdminTokenService.MinSigningKeyLength)
            {
                throw new InvalidOperationException($"{ConfigKey} 長度不足 {AdminTokenService.MinSigningKeyLength} 字元，無法安全簽發會員登入權杖。");
            }

            return Encoding.UTF8.GetBytes(dedicated);
        }

        AdminTokenService.ValidateSigningKeyConfigured(configuration);
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: Encoding.UTF8.GetBytes(configuration[AdminTokenService.ConfigKey]!),
            outputLength: 32,
            salt: null,
            info: Encoding.UTF8.GetBytes(DerivationLabel));
    }

    private SymmetricSecurityKey SigningKey => new(DeriveKeyBytes(configuration));

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(Guid memberId)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(AccessTokenLifetime);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, memberId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
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
        NameClaimType = JwtRegisteredClaimNames.Sub,
    };
}
