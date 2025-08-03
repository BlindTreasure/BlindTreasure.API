using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IReviewService
{
    Task<ReviewResponseDto> CreateReviewAsync(Guid userId, CreateReviewDto createDto);
    Task<Pagination<ReviewResponseDto>> GetAllReviewsAsync(ReviewQueryParameter param);
    Task<ReviewResponseDto> GetReviewByIdAsync(Guid reviewId);
    
}