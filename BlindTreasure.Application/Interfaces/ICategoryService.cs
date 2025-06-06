﻿using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ICategoryService
{
    Task<CategoryDto> CreateAsync(CategoryCreateDto dto);
    Task<CategoryDto> DeleteAsync(Guid id);
    Task<Pagination<CategoryDto>> GetAllAsync(CategoryQueryParameter param);
    Task<CategoryDto?> GetByIdAsync(Guid id);
    Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateDto dto);
}