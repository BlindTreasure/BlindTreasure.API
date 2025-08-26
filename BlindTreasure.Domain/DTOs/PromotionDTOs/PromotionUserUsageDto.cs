using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs;

public class PromotionUserUsageDto
{
    public Guid Id { get; set; }
    public Guid? PromotionId { get; set; }
    public PromotionDto? Promotion { get; set; }

    public Guid UserId { get; set; }
    public UserDto? User { get; set; }

    public int UsageCount { get; set; } = 0;

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    // Computed property, không map vào DB
    public bool? IsMaxUsageReached { get; set; }
}