namespace BlindTreasure.Domain.Entities;

public class Address : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string FullName { get; set; }
    public string Phone { get; set; }
    public string AddressLine { get; set; }
    public string City { get; set; }
    //thiếu ward tên phường/xã
    //thiếu district tên quận/huyện
    // thiếu street 
    public string Province { get; set; }
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "Vietnam";
    public bool IsDefault { get; set; } = false;

    // 1-n → Orders
    public ICollection<Order> Orders { get; set; }
}