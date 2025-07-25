using System.ComponentModel.DataAnnotations;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;

public class CustomerFavouriteDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? BlindBoxId { get; set; }
    public string Type { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ProducDetailDto? Product { get; set; }
    public BlindBoxDetailDto? BlindBox { get; set; }
}

public class AddFavouriteRequestDto
{
    public Guid? ProductId { get; set; }

    public Guid? BlindBoxId { get; set; }

    [Required] public FavouriteType Type { get; set; } // "Product" hoặc "BlindBox"
}

