namespace BlindTreasure.Domain.Enums;

public enum StockStatus
{
    InStock, // TotalQuantity > 0
    OutOfStock // TotalQuantity <= 0
}