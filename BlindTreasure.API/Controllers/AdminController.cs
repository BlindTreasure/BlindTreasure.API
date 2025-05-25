using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ApiController]
[Route("api/admin")] // hoặc "api/seller-verification"
public class AdminController : ControllerBase
{
    private readonly ISellerVerificationService _sellerVerificationService;

    public AdminController(ISellerVerificationService sellerVerificationService)
    {
        _sellerVerificationService = sellerVerificationService;
    }
}