using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberDraws;

public static class MemberDrawsEndpoints
{
    public static void MapMemberDrawsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/member/draws?lang=zh —— 會員可見的抽獎活動與「自己的」資格布林（App 規劃書 §3.10，唯讀）。
        // 逐俱樂部：所有俱樂部已鎖定名單之後的活動都列出，IsEligible 是「你在該活動主辦俱樂部的會籍是否涵蓋基準時間」。
        // 🔴 會籍與抽獎資格不得讀快取（docs/14）：每次即時計算、回應 no-store，也不在 ETag 白名單內。
        app.MapGet("/api/v1/member/draws", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MemberDrawsService service, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(await service.ListAsync(me.MemberId, lang == "en" ? "en" : "zh", ct));
        })
        .WithName("GetMyDraws")
        .WithTags("MemberDraws")
        .Produces<IReadOnlyList<MemberDrawDto>>()
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
