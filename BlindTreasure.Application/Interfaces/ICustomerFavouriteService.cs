using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ICustomerFavouriteService
{
    Task<CustomerFavouriteDto> AddToFavouriteAsync(AddFavouriteRequestDto request);
    Task RemoveFromFavouriteAsync(Guid favouriteId);
    Task<FavouriteListResponseDto> GetUserFavouritesAsync(int page = 1, int pageSize = 10);
    Task<bool> IsInFavouriteAsync(Guid? productId, Guid? blindBoxId);
}