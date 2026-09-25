using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Forms;

/// <summary>10 表單中心：7 類表單 ＋ 提案下載 ＋ 捐助洽詢的公開讀取與送出端點（主站規劃書 §3.10）。
/// 全部不需要登入。<see cref="RateLimitPolicyName"/> 的濫用防護設計見 <c>Program.cs</c> 與
/// <c>FormsRepository</c> 檔頭。</summary>
public static class FormsEndpoints
{
    /// <summary>與 <c>Program.cs</c> 註冊的 Rate Limiting 具名政策一致，改名要兩處一起改。</summary>
    public const string RateLimitPolicyName = "form-submission";

    public static void MapFormsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/forms/{formCode}?lang=zh|en
        app.MapGet("/api/v1/{club}/forms/{formCode}", async (
            string club, string formCode, string? lang, IClubResolver clubResolver, FormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var form = await repository.GetFormDefinitionAsync(scope, formCode, dbLocale, cancellationToken);
            return form is null ? Results.NotFound() : Results.Ok(form);
        })
        .WithName("GetPublicForm")
        .WithTags("Forms")
        .Produces<PublicFormDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/forms/{formCode}/submissions
        app.MapPost("/api/v1/{club}/forms/{formCode}/submissions", async (
            string club, string formCode, SubmitFormRequest request,
            IClubResolver clubResolver, FormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.SubmitAsync(scope, formCode, request, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("SubmitPublicForm")
        .WithTags("Forms")
        .RequireRateLimiting(RateLimitPolicyName)
        .Produces<SubmitFormResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);
    }
}
