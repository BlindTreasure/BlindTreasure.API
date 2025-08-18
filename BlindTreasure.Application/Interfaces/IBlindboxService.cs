using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IBlindBoxService
{
    Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId);
    Task<Pagination<BlindBoxDetailDto>> GetAllBlindBoxesAsync(BlindBoxQueryParameter param);
    Task<BlindBoxDetailDto> CreateBlindBoxAsync(CreateBlindBoxDto dto);
    Task<BlindBoxDetailDto> UpdateBlindBoxAsync(Guid blindBoxId, UpdateBlindBoxDto dto);
    Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemRequestDto> items);
    Task<BlindBoxDetailDto> SubmitBlindBoxAsync(Guid blindBoxId);
    Task<BlindBoxDetailDto> ReviewBlindBoxAsync(Guid blindBoxId, bool approve, string? rejectReason = null);
    Task<BlindBoxDetailDto> ClearItemsFromBlindBoxAsync(Guid blindBoxId);
    Task<BlindBoxDetailDto> DeleteBlindBoxAsync(Guid blindBoxId);
    Dictionary<BlindBoxItemRequestDto, decimal> CalculateDropRates(List<BlindBoxItemRequestDto> items);
}