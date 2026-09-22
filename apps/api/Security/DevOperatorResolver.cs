using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>唯一實作，見 <see cref="IDevOperatorResolver"/> 上的完整說明——不是身分驗證。</summary>
public sealed class DevOperatorResolver(ClubDbContext dbContext) : IDevOperatorResolver
{
    public const string HeaderName = "X-Dev-Operator-Id";

    public async Task<Guid?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return null;
        }

        if (!Guid.TryParse(headerValues.ToString(), out var operatorId))
        {
            return null;
        }

        var exists = await dbContext.AdminUsers.AsNoTracking()
            .AnyAsync(u => u.Id == operatorId, cancellationToken);

        return exists ? operatorId : null;
    }
}
