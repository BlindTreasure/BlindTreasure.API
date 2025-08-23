using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs;

public class SellerStatisticsRequestDto
{
    [DefaultValue(StatisticsTimeRange.Custom)]
    [Required] public StatisticsTimeRange Range { get; set; } = StatisticsTimeRange.Week;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Validation: Custom range requires StartDate and EndDate
    public bool IsValid()
    {
        if (Range == StatisticsTimeRange.Custom) return StartDate.HasValue && EndDate.HasValue && StartDate <= EndDate;
        return true;
    }
}