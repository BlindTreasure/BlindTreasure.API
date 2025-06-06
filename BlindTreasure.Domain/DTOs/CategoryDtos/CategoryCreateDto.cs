using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Domain.DTOs.CategoryDtos;

public class CategoryCreateDto
{
    [MaxLength(100)]
    [DefaultValue("BabyThree")]
    public required string Name { get; set; }

    [MaxLength(255)]
    [DefaultValue("Danh mục con dành cho sản phẩm trẻ em, thuộc nhóm phân loại chính.")]
    public string? Description { get; set; }
    
    public IFormFile? ImageFile { get; set; }

    public Guid? ParentId { get; set; }
}