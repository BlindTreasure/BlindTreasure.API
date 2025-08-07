using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IReviewService
{
    // Task<ReviewResponseDto> DeleteReviewAsync(Guid id);
    // seller không được xóa, chỉ có customer tự xóa comment của miình

    // Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param); 
    // lọc các review của 1 product/ blindbox
    // lọc theo rating
    // lọc theo có comment hay không (có những review không cần comment)


    Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto);
    Task<ReviewResponseDto> ReplyToReviewAsync(Guid reviewId, string replyContent);
    Task<bool> CanReviewOrderDetailAsync(Guid orderDetailId);
    Task<ReviewResponseDto> GetByIdAsync(Guid reviewId);
}