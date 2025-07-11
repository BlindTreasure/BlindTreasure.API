using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class SellerServiceTests
{
    private readonly Mock<IBlobService> _blobServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IMapperService> _mapperServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock; // thêm dòng này
    private readonly Mock<IGenericRepository<Product>> _productRepoMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<IGenericRepository<Seller>> _sellerRepoMock;
    private readonly SellerService _sellerService;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;


    public SellerServiceTests()
    {
        _blobServiceMock = new Mock<IBlobService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheServiceMock = new Mock<ICacheService>();
        _mapperServiceMock = new Mock<IMapperService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _productServiceMock = new Mock<IProductService>();
        _notificationServiceMock = new Mock<INotificationService>(); // thêm dòng này
        _sellerRepoMock = new Mock<IGenericRepository<Seller>>();
        _productRepoMock = new Mock<IGenericRepository<Product>>();

        _unitOfWorkMock.Setup(x => x.Sellers).Returns(_sellerRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Products).Returns(_productRepoMock.Object);

        _sellerService = new SellerService(
            _blobServiceMock.Object,
            _emailServiceMock.Object,
            _loggerServiceMock.Object,
            _unitOfWorkMock.Object,
            _cacheServiceMock.Object,
            _mapperServiceMock.Object,
            _claimsServiceMock.Object,
            _productServiceMock.Object,
            _notificationServiceMock.Object // thêm dòng này
        );
    }

    [Fact]
    public async Task UpdateSellerInfoAsync_UpdatesSeller_WhenValid()
    {
        var userId = Guid.NewGuid();
        var seller = new Seller
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Id = userId,
                FullName = "Old",
                Email = "seller@email.com",
                RoleName = RoleType.Seller // Set the required 'RoleName' property
            }
        };
        var dto = new UpdateSellerInfoDto
        {
            FullName = "New Name",
            PhoneNumber = "0123456789",
            DateOfBirth = DateTime.UtcNow.AddYears(-20),
            CompanyName = "Company",
            TaxId = "123",
            CompanyAddress = "Addr"
        };

        _unitOfWorkMock.Setup(x => x.Sellers.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Seller, bool>>>(),
            It.IsAny<Expression<Func<Seller, object>>[]>()
        ));

        _unitOfWorkMock.Setup(x => x.Sellers.Update(seller)).ReturnsAsync(true);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sellerService.UpdateSellerInfoAsync(userId, dto);

        Assert.NotNull(result);
        _unitOfWorkMock.Verify(x => x.Sellers.Update(seller), Times.Once);
        _cacheServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), seller, It.IsAny<TimeSpan>()), Times.Exactly(2));
    }
}