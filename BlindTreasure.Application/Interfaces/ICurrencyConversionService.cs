using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ICurrencyConversionService
    {
        Task<decimal?> GetVNDToUSDRate();
    }
}
