namespace BlindTreasure.Domain.DTOs.CategoryDtos;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsDeleted { get; set; }

    // Audit fields
    public DateTime? CreatedAt { get; set; }
    public List<CategoryDto> Children { get; set; } = new();
}