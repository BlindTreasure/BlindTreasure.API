﻿namespace BlindTreasure.Domain.DTOs.AddressDTOs;

public class UpdateAddressDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
}