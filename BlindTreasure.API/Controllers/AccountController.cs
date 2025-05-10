using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AccountDTOs;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    ///     Đăng ký tài khoản mới.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] UserRegistrationDto registrationDto)
    {
        if (registrationDto == null) return BadRequest(ApiResult.Failure("400", "Invalid input data."));

        var result = await _accountService.RegisterUserAsync(registrationDto);

        if (result) return Ok(ApiResult.Success("201", "User registered successfully."));

        return BadRequest(ApiResult.Failure("400", "User registration failed."));
    }
}