﻿namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class OrderAddressDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string AddressLine { get; set; }
    public string City { get; set; }
    public string Province { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
}