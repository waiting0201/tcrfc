using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>唯讀：列出 G-12 掛載點字典（S1-7a）。沒有 club_id、沒有寫入端點——見
/// <c>AdminFaqEmbedSlotDtos.cs</c> 檔頭說明。</summary>
public sealed class AdminFaqEmbedSlotsRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminFaqEmbedSlotDto>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.FaqEmbedSlots.AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new AdminFaqEmbedSlotDto { Id = s.Id, Code = s.Code, Name = s.Name })
            .ToListAsync(cancellationToken);
    }
}
