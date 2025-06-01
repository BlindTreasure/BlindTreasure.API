using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services.Commons;

public class DataAnalyzerService : IDataAnalyzerService
{
    private readonly IUnitOfWork _unitOfWork;

    public DataAnalyzerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    
    public async Task<List<UserDto>> GetUsersForAiAnalysisAsync()
    {
        var users = await _unitOfWork.Users.GetQueryable()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Take(20) 
            .ToListAsync();
        var userDtos = users.Select(UserMapper.ToUserDto).ToList();

        return userDtos;
    }
    //
}