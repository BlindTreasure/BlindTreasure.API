using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.AddressDTOs
{
    public class CreateAddressDto
    {
        [DefaultValue("Quang wibu")]
        public string FullName { get; set; }
        [DefaultValue("0123456789")]
        public string Phone { get; set; }
        [DefaultValue("123 Le Loi")]
        public string AddressLine1 { get; set; }
        [DefaultValue("HCM City")]
        public string City { get; set; }
        [DefaultValue("Ho Chi Minh")]
        public string Province { get; set; } = "Viet Nam";
        public string? PostalCode { get; set; } = "700000"; // Default value for PostalCode HCM CITY
        public bool IsDefault { get; set; } = false; // Default value for IsDefault
    }
}
