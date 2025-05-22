using BlindTreasure.Application.Interfaces;
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