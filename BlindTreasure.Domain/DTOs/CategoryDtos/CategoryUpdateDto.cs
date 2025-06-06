using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.CategoryDtos;

public class CategoryUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public IFormFile? ImageFile { get; set; }
    public Guid? ParentId { get; set; }
}