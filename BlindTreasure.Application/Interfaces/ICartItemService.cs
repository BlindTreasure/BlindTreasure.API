using BlindTreasure.Domain.DTOs.CartItemDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ICartItemService
    {
        Task<CartDto> AddToCartAsync(AddCartItemDto dto);
        Task ClearCartAsync();
        Task<CartDto> GetCurrentUserCartAsync();
        Task<CartDto> RemoveCartItemAsync(Guid cartItemId);
        Task<CartDto> UpdateCartItemAsync(UpdateCartItemDto dto);
    }

}
