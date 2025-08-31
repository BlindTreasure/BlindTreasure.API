namespace BlindTreasure.Application.Interfaces;

public interface ICurrencyConversionService
{
    Task<decimal?> GetVNDToUSDRate();
}