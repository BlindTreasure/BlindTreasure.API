namespace BlindTreasure.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string? ImageUrl { get; set; } // đường dẫn ảnh


    // FK self-reference
    public Guid? ParentId { get; set; }
    public Category Parent { get; set; }

    // 1-n → Children, Products
    public ICollection<Category> Children { get; set; }
    public ICollection<Product> Products { get; set; }
}