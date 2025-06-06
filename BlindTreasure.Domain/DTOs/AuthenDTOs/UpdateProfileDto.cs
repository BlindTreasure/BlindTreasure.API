﻿namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class UpdateProfileDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool? Gender { get; set; }
}

public class UpdateAvatarResultDto
{
    public string? AvatarUrl { get; set; }
}