namespace BlindTreasure.Application.Utils.SharedCacheKeys;

public static class ListingSharedCacheKeys
{
    // Inventory item chi tiết
    public static string GetInventoryItem(Guid inventoryItemId)
    {
        return $"inventoryitem:{inventoryItemId}";
    }

    // Key cho danh sách items khả dụng của 1 user (ListingService dùng key kiểu "listing:user-items:{userId}")
    public static string GetUserAvailableItems(Guid userId)
    {
        return $"listing:user-items:{userId}";
    }

    // Listing detail (nếu cần đồng bộ tên key giữa service)
    public static string GetListingDetail(Guid listingId)
    {
        return $"listing:detail:{listingId}";
    }
}