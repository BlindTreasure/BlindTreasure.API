using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ReviewDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IBlindyService _blindyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoggerService _loggerService;
    private readonly IClaimsService _claimService;

    public ReviewService(IBlindyService blindyService, IUnitOfWork unitOfWork, ILoggerService loggerService,
        IClaimsService claimService)
    {
        _blindyService = blindyService;
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _claimService = claimService;
    }

    // public async Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto createDto)
    // {
    //     try
    //     {
    //         var userId = _claimService.CurrentUserId;
    //         _loggerService.Info($"Creating review for UserId: {userId}, OrderDetailId: {createDto.OrderDetailId}");
    //
    //        
    //     }
    //     catch (Exception ex)
    //     {
    //       
    //     }
    // }



    #region private methods
  
    #endregion
}