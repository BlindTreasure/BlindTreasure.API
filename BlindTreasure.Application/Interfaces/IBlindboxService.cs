using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IBlindBoxService
{
    Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId);
    Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param);
    Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto);
    Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemDto> items);
    Task<BlindBoxDetailDto> SubmitBlindBoxAsync(Guid blindBoxId);

    Task<List<BlindBoxDetailDto>> GetPendingApprovalBlindBoxesAsync();
    Task<BlindBoxDetailDto> ApproveBlindBoxAsync(Guid blindBoxId);
    Task<BlindBoxDetailDto> RejectBlindBoxAsync(Guid blindBoxId, string reason);
}