using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Domain.Pagination;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;

    public AdminController(IUserService userService, IClaimsService claimsService)
    {
        _userService = userService;
        _claimsService = claimsService;
    }

   
}