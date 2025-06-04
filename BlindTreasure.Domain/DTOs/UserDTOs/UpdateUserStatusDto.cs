using System.ComponentModel.DataAnnotations;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.UserDTOs;

public class UpdateUserStatusDto
{
    [Required] public UserStatus Status { get; set; }

    public string? Reason { get; set; }
}