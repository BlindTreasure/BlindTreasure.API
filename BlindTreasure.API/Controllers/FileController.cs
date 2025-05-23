using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("[controller]")]
public class FileController : ControllerBase
{
    private readonly IBlobService _blobService;

    public FileController(IBlobService blobService)
    {
        _blobService = blobService;
    }

    /// <summary>
    /// Upload file lên MinIO.
    /// </summary>
    /// <param name="file">File cần upload</param>
    /// <returns>URL preview hoặc lỗi</returns>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File không hợp lệ.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await _blobService.UploadFileAsync(file.FileName, stream);
            var previewUrl = await _blobService.GetPreviewUrlAsync(file.FileName);
            return Ok(new { url = previewUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Upload thất bại: {ex.Message}");
        }
    }
}