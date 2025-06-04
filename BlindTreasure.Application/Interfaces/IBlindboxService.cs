using BlindTreasure.Domain.DTOs.BlindBoxDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IBlindBoxService
{
    Task<BlindBoxDetailDto> GetBlindBoxByIdAsync(Guid blindBoxId);
    Task<BlindBoxDetailDto> AddItemsToBlindBoxAsync(Guid blindBoxId, List<BlindBoxItemDto> items);
    Task<bool> SubmitBlindBoxAsync(Guid blindBoxId);
}