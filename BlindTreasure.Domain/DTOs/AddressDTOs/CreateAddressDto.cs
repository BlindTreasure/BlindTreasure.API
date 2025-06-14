using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.AddressDTOs
{
    public class CreateAddressDto
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string AddressLine1 { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string? PostalCode { get; set; } = "700000"; // Default value for PostalCode HCM CITY
        public bool IsDefault { get; set; } = false; // Default value for IsDefault
    }
}
