namespace BlindTreasure.Domain.Enums;

public enum ReviewStatus
{
    PendingValidation, // Chờ AI validate
    Approved, // AI đã duyệt, hiển thị công khai
    RequiresModeration, // AI phát hiện vấn đề, cần staff xem
    Rejected, // AI từ chối hoàn toàn
    Hidden // Bị ẩn sau khi publish
}