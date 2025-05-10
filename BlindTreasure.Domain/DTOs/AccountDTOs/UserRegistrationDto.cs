using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AccountDTOs;

public class UserRegistrationDto
{
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public string Email { get; set; }

    [DefaultValue("Cubin2003@")] public string Password { get; set; }

    [DefaultValue("phuc")] public string FullName { get; set; }
}