using BlindTreasure.Application.Utils;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    // Ví dụ hardcoded cho đơn giản
    private static readonly List<string> Products = new() { "Sneaker A", "Labubu B", "Anime C" };

    [HttpGet("{id}")]
    public IActionResult GetProduct(int id)
    {
        if (id < 0 || id >= Products.Count)
        {
            return BadRequest(ApiResult<string>.Failure("404", "Sản phẩm không tồn tại."));
        }

        var product = Products[id];
        return Ok(ApiResult<string>.Success(product, "200", "Lấy thông tin sản phẩm thành công."));
    }

    [HttpGet("health-check")]
    public IActionResult HealthCheck()
    {
        return Ok(ApiResult.Success("200", "Hệ thống hoạt động ổn định."));
    }
}