using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> CreateAsync(ProductCreateDto dto);
        Task<ProductDto> DeleteAsync(Guid id);
        Task<Pagination<ProductDto>> GetAllAsync(PaginationParameter param);
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> UpdateAsync(Guid id, ProductUpdateDto dto);
    }
}
