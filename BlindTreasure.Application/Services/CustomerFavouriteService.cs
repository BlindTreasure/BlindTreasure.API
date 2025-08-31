using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class CustomerFavouriteService : ICustomerFavouriteService
{
    private readonly IClaimsService _claimsService;
    private readonly IMapperService _mapperService;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerFavouriteService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IMapperService mapperService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _mapperService = mapperService;
    }

    public async Task<CustomerFavouriteDto> AddToFavouriteAsync(AddFavouriteRequestDto request)
    {
        var currentUserId = _claimsService.CurrentUserId;
        if (currentUserId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Vui lòng đăng nhập để thêm vào danh sách yêu thích.");

        // Validate input - cả ProductId và BlindBoxId không được cùng null
        if (request.ProductId == null && request.BlindBoxId == null)
            throw ErrorHelper.BadRequest("Vui lòng chọn ít nhất một sản phẩm hoặc blind box để thêm vào yêu thích.");

        // Check if already exists
        var existingFavourite = await _unitOfWork.CustomerFavourites
            .FirstOrDefaultAsync(cf => cf.UserId == currentUserId &&
                                       cf.ProductId == request.ProductId &&
                                       cf.BlindBoxId == request.BlindBoxId &&
                                       !cf.IsDeleted);

        if (existingFavourite != null)
            throw ErrorHelper.Conflict("Sản phẩm này đã có trong danh sách yêu thích.");

        // Verify product/blindbox exists
        if (request.ProductId != null)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId.Value);
            if (product == null || product.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy sản phẩm.");
        }

        if (request.BlindBoxId != null)
        {
            var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(request.BlindBoxId.Value);
            if (blindBox == null || blindBox.IsDeleted)
                throw ErrorHelper.NotFound("Không tìm thấy blind box.");
        }

        // Create favourite
        var favourite = new CustomerFavourite
        {
            UserId = currentUserId,
            ProductId = request.ProductId,
            BlindBoxId = request.BlindBoxId,
            Type = request.Type
        };

        await _unitOfWork.CustomerFavourites.AddAsync(favourite);
        await _unitOfWork.SaveChangesAsync();

        // Load với navigation properties để map
        var createdFavourite = await _unitOfWork.CustomerFavourites
            .FirstOrDefaultAsync(cf => cf.Id == favourite.Id,
                cf => cf.Product,
                cf => cf.BlindBox);

        // Sử dụng phương thức map riêng thay vì dùng _mapperService
        return MapToCustomerFavouriteDto(createdFavourite);
    }

    public async Task RemoveFromFavouriteAsync(Guid favouriteId)
    {
        var currentUserId = _claimsService.CurrentUserId;
        if (currentUserId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Vui lòng đăng nhập.");

        var favourite = await _unitOfWork.CustomerFavourites
            .FirstOrDefaultAsync(cf => cf.Id == favouriteId &&
                                       cf.UserId == currentUserId &&
                                       !cf.IsDeleted);

        if (favourite == null)
            throw ErrorHelper.NotFound("Không tìm thấy item yêu thích.");

        await _unitOfWork.CustomerFavourites.SoftRemove(favourite);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Pagination<CustomerFavouriteDto>> GetUserFavouritesAsync(FavouriteQueryParameter param)
    {
        var currentUserId = _claimsService.CurrentUserId;
        if (currentUserId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Vui lòng đăng nhập.");

        var query = _unitOfWork.CustomerFavourites.GetQueryable()
            .Include(cf => cf.Product)
            .ThenInclude(p => p.Category)
            .Include(cf => cf.Product)
            .ThenInclude(p => p.Seller)
            .Include(cf => cf.BlindBox)
            .ThenInclude(b => b.Category)
            .Include(cf => cf.BlindBox)
            .ThenInclude(b => b.Seller)
            .Where(cf => cf.UserId == currentUserId && !cf.IsDeleted)
            .AsNoTracking();

        // Tính tổng số bản ghi
        var count = await query.CountAsync();

        // Sắp xếp theo thời gian tạo
        query = param.Desc
            ? query.OrderByDescending(cf => cf.CreatedAt)
            : query.OrderBy(cf => cf.CreatedAt);

        // Phân trang
        var favourites = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        // Chuyển đổi sang DTO
        var favouriteDtos = favourites.Select(MapToCustomerFavouriteDto).ToList();

        return new Pagination<CustomerFavouriteDto>(favouriteDtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<bool> IsInFavouriteAsync(Guid? productId, Guid? blindBoxId)
    {
        var currentUserId = _claimsService.CurrentUserId;
        if (currentUserId == Guid.Empty)
            return false;

        return await _unitOfWork.CustomerFavourites
            .GetQueryable()
            .AnyAsync(cf => cf.UserId == currentUserId &&
                            cf.ProductId == productId &&
                            cf.BlindBoxId == blindBoxId &&
                            !cf.IsDeleted);
    }

    private CustomerFavouriteDto MapToCustomerFavouriteDto(CustomerFavourite cf)
    {
        return new CustomerFavouriteDto
        {
            Id = cf.Id,
            UserId = cf.UserId,
            ProductId = cf.ProductId,
            BlindBoxId = cf.BlindBoxId,
            Type = cf.Type.ToString(),
            CreatedAt = cf.CreatedAt,
            Product = cf.Product != null
                ? new ProducDetailDto
                {
                    Id = cf.Product.Id,
                    Name = cf.Product.Name,
                    Description = cf.Product.Description,
                    RealSellingPrice = cf.Product.RealSellingPrice,
                    CategoryId = cf.Product.CategoryId,
                    ImageUrls = cf.Product.ImageUrls,
                    SellerId = cf.Product.SellerId,
                    Status = cf.Product.Status,
                    ProductType = cf.Product.ProductType,
                    TotalStockQuantity = cf.Product.TotalStockQuantity,
                    ProductStockStatus =
                        cf.Product.TotalStockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock
                }
                : null,
            BlindBox = cf.BlindBox != null
                ? new BlindBoxDetailDto
                {
                    Id = cf.BlindBox.Id,
                    Name = cf.BlindBox.Name,
                    Description = cf.BlindBox.Description,
                    Price = cf.BlindBox.Price,
                    ImageUrl = cf.BlindBox.ImageUrl,
                    CategoryId = cf.BlindBox.CategoryId,
                    CategoryName = cf.BlindBox.Category?.Name,
                    Status = cf.BlindBox.Status,
                    TotalQuantity = cf.BlindBox.TotalQuantity,
                    BlindBoxStockStatus = cf.BlindBox.TotalQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock,
                    Brand = cf.BlindBox.Brand,
                    BindBoxTags = cf.BlindBox.BindBoxTags
                }
                : null
        };
    }
}