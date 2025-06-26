using BlindTreasure.Domain.DTOs.ProductDTOs;

namespace BlindTreasure.Domain.DTOs.CategoryDtos;

public class CategoryWithProductsDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public int ProductCount { get; set; } // số lượng sản phẩm
    public List<ProducDetailDto> Products { get; set; } = new();
}