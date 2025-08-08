using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IReviewService
{
    Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param);
    Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto);
    Task<ReviewResponseDto> ReplyToReviewAsync(Guid reviewId, string replyContent);
    Task<bool> HasReviewedOrderDetailAsync(Guid orderDetailId);
    Task<ReviewResponseDto> GetByIdAsync(Guid reviewId);
    Task<ReviewResponseDto> DeleteReviewAsync(Guid reviewId);
}