using System.ComponentModel.DataAnnotations;

namespace BlindTreasure.Domain.Entities;

public class Address : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(255)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(255)]
    public string AddressLine2 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}