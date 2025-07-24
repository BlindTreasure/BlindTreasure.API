using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class CustomerFavouriteService : ICustomerFavouriteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IMapperService _mapperService;

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

        return _mapperService.Map<CustomerFavourite, CustomerFavouriteDto>(createdFavourite);
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
            .ThenInclude(b => b.Seller)
            .Where(cf => cf.UserId == currentUserId && !cf.IsDeleted)
            .AsNoTracking();

        // Sắp xếp theo thời gian tạo
        query = param.Desc 
            ? query.OrderByDescending(cf => cf.CreatedAt) 
            : query.OrderBy(cf => cf.CreatedAt);

        var count = await query.CountAsync();

        var favourites = await query
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var favouriteDtos = favourites
            .Select(f => _mapperService.Map<CustomerFavourite, CustomerFavouriteDto>(f))
            .ToList();

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
}