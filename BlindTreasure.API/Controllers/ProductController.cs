using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }


        /// <summary>
        /// Lấy danh sách sản phẩm của Seller (có phân trang).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<Pagination<ProductDto>>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParameter param)
        {
            try
            {
                var result = await _productService.GetAllAsync(param);
                return Ok(ApiResult<object>.Success(new
                {
                    result,
                    count = result.TotalCount,
                    pageSize = result.PageSize,
                    currentPage = result.CurrentPage,
                    totalPages = result.TotalPages
                }, "200", "Lấy danh sách sản phẩm thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }


        /// <summary>
        /// Lấy chi tiết sản phẩm theo Id.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _productService.GetByIdAsync(id);
                return Ok(ApiResult<ProductDto>.Success(result, "200", "Lấy thông tin sản phẩm thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

        /// <summary>
        /// đăngg ký sản phẩm mới.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
        public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
        {
            try
            {
                var result = await _productService.CreateAsync(dto);
                return Ok(ApiResult<ProductDto>.Success(result, "200", "Tạo sản phẩm thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

        // <summary>
        /// Cập nhật sản phẩm.
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] ProductUpdateDto dto)
        {
            try
            {
                var result = await _productService.UpdateAsync(id, dto);
                return Ok(ApiResult<ProductDto>.Success(result, "200", "Cập nhật sản phẩm thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

        /// <summary>
        /// Xóa mềm sản phẩm.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _productService.DeleteAsync(id);
                return Ok(ApiResult<ProductDto>.Success(result, "200", "Xóa sản phẩm thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

    }
}
