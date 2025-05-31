using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.DTOs.CategoryDtos;

public class CategoryCreateDto
{
    [Required] [MaxLength(100)] public string Name { get; set; }

    //  [Required]
    [MaxLength(255)] public string Description { get; set; }
    public Guid? ParentId { get; set; }
}