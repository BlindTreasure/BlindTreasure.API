namespace BlindTreasure.Domain.DTOs.AccountDTOs;

public class UserRegistrationDto
{
    // Địa chỉ email của người dùng
    public string Email { get; set; }

    // Mật khẩu của người dùng
    public string Password { get; set; }

    // Họ và tên người dùng
    public string FullName { get; set; }

    // Bạn có thể thêm các trường khác như phone, address, v.v.
}
