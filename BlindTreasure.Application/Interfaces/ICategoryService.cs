using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto> CreateAsync(CategoryCreateDto dto);
        Task<CategoryDto> DeleteAsync(Guid id);
        Task<Pagination<CategoryDto>> GetAllAsync(PaginationParameter param);
        Task<CategoryDto?> GetByIdAsync(Guid id);
        Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateDto dto);
    }
}
