namespace BlindTreasure.Domain.Enums;

public enum BlindBoxStockStatus
{
    InStock,       // TotalQuantity > 0
    OutOfStock     // TotalQuantity <= 0
}
