using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IReviewService
{
    Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto);
    Task<ReviewResponseDto> ReplyToReviewAsync(Guid reviewId, string replyContent);
    Task<bool> CanReviewOrderDetailAsync(Guid orderDetailId);
    Task<ReviewResponseDto> GetByIdAsync(Guid reviewId);
}