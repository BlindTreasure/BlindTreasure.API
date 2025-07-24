using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ICustomerFavouriteService
{
    Task<CustomerFavouriteDto> AddToFavouriteAsync(AddFavouriteRequestDto request);
    Task RemoveFromFavouriteAsync(Guid favouriteId);
    Task<Pagination<CustomerFavouriteDto>> GetUserFavouritesAsync(FavouriteQueryParameter param);
    Task<bool> IsInFavouriteAsync(Guid? productId, Guid? blindBoxId);
}