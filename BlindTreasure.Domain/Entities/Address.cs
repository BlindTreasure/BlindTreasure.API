namespace BlindTreasure.Domain.Entities;

public class Address : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string FullName { get; set; }
    public string Phone { get; set; }
    // field dùng GHN
    public string AddressLine { get; set; } // vd: "123 Đường ABC, Phường XYZ, Quận 10, HCM"
    public string City { get; set; } = "Ho Chi Minh City"; // Default city
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string Province { get; set; } = "Ho Chi Minh City"; // Default province, hoặc tỉnh và thành phố khác tỉnh
    //
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "Vietnam";
    public bool IsDefault { get; set; } = false;

    // 1-n → Orders
    public ICollection<Order> Orders { get; set; }
}