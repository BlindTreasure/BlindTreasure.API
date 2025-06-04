using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Moq;

namespace BlindTreaure.UnitTest.Services
{
    public class CategoryServiceTests
    {
        private readonly CategoryService _categoryService;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private readonly Mock<IClaimsService> _claimsServiceMock;
        private readonly Mock<ILoggerService> _loggerMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserService> _userServiceMock;
        private readonly Mock<IGenericRepository<Category>> _categoryRepoMock;

        public CategoryServiceTests()
        {
            _cacheServiceMock = new Mock<ICacheService>();
            _claimsServiceMock = new Mock<IClaimsService>();
            _loggerMock = new Mock<ILoggerService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userServiceMock = new Mock<IUserService>();
            _categoryRepoMock = new Mock<IGenericRepository<Category>>();

            _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepoMock.Object);

            _categoryService = new CategoryService(
                _unitOfWorkMock.Object,
                _loggerMock.Object,
                _cacheServiceMock.Object,
                _claimsServiceMock.Object,
                _userServiceMock.Object
            );
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCategoryDto_WhenFoundInCache()
        {
            var id = Guid.NewGuid();
            var category = new Category { Id = id, Name = "Test", Description = "Desc" };
            _cacheServiceMock.Setup(x => x.GetAsync<Category>($"category:{id}")).ReturnsAsync(category);

            var result = await _categoryService.GetByIdAsync(id);

            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            _cacheServiceMock.Verify(x => x.GetAsync<Category>($"category:{id}"), Times.Once);
            _categoryRepoMock.Verify(x => x.GetQueryable(), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCategoryDto_WhenFoundInDb()
        {
            var id = Guid.NewGuid();
            var category = new Category { Id = id, Name = "Test", Description = "Desc" };
            _cacheServiceMock.Setup(x => x.GetAsync<Category>($"category:{id}")).ReturnsAsync((Category)null!);
            _categoryRepoMock.Setup(x => x.GetQueryable())
                .Returns(new List<Category> { category }.AsQueryable());

            var result = await _categoryService.GetByIdAsync(id);

            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            _cacheServiceMock.Verify(x => x.SetAsync($"category:{id}", category, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ThrowsNotFound_WhenNotFound()
        {
            var id = Guid.NewGuid();
            _cacheServiceMock.Setup(x => x.GetAsync<Category>($"category:{id}")).ReturnsAsync((Category)null!);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category>().AsQueryable());

            await Assert.ThrowsAsync<Exception>(() => _categoryService.GetByIdAsync(id));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsPagination()
        {
            var param = new CategoryQueryParameter { PageIndex = 1, PageSize = 10 };
            var categories = new List<Category>
            {
                new Category { Id = Guid.NewGuid(), Name = "A", Description = "A" },
                new Category { Id = Guid.NewGuid(), Name = "B", Description = "B" }
            };
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(categories.AsQueryable());

            var result = await _categoryService.GetAllAsync(param);

            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count); // Pagination<T> kế thừa List<T>
        }

        [Fact]
        public async Task CreateAsync_CreatesCategory_WhenValid()
        {
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var dto = new CategoryCreateDto { Name = "NewCat", Description = "Desc" };
            var category = new Category { Id = Guid.NewGuid(), Name = dto.Name, Description = dto.Description };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category>().AsQueryable());
            _categoryRepoMock.Setup(x => x.AddAsync(It.IsAny<Category>())).ReturnsAsync(category);
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            var result = await _categoryService.CreateAsync(dto);

            Assert.NotNull(result);
            Assert.Equal(dto.Name, result.Name);
            _categoryRepoMock.Verify(x => x.AddAsync(It.IsAny<Category>()), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveByPatternAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ThrowsConflict_WhenNameExists()
        {
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var dto = new CategoryCreateDto { Name = "DupCat", Description = "Desc" };
            var categories = new List<Category> { new Category { Name = dto.Name, IsDeleted = false } };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(categories.AsQueryable());

            await Assert.ThrowsAsync<Exception>(() => _categoryService.CreateAsync(dto));
        }

        [Fact]
        public async Task UpdateAsync_UpdatesCategory_WhenValid()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var category = new Category { Id = id, Name = "Old", Description = "OldDesc", IsDeleted = false };
            var dto = new CategoryUpdateDto { Name = "NewName", Description = "NewDesc" };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category> { category }.AsQueryable());
            _unitOfWorkMock.Setup(x => x.Categories.Update(category)).ReturnsAsync(true);
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            var result = await _categoryService.UpdateAsync(id, dto);

            Assert.NotNull(result);
            Assert.Equal(dto.Name, result.Name);
            _unitOfWorkMock.Verify(x => x.Categories.Update(category), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveAsync(It.IsAny<string>()), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveByPatternAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsNotFound_WhenCategoryNotFound()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var dto = new CategoryUpdateDto { Name = "NewName" };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category>().AsQueryable());

            await Assert.ThrowsAsync<Exception>(() => _categoryService.UpdateAsync(id, dto));
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesCategory_WhenValid()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var category = new Category { Id = id, Name = "Cat", IsDeleted = false, Products = new List<Product>(), Children = new List<Category>() };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category> { category }.AsQueryable());
            _unitOfWorkMock.Setup(x => x.Categories.SoftRemove(category)).ReturnsAsync(true); // Fix: Ensure the return type matches Task<bool>
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            var result = await _categoryService.DeleteAsync(id);

            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            _unitOfWorkMock.Verify(x => x.Categories.SoftRemove(category), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveAsync(It.IsAny<string>()), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveByPatternAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsConflict_WhenCategoryHasProductsOrChildren()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userDto = new UserDto { UserId = userId, FullName = "Admin", RoleName = RoleType.Admin, Email = "admin@example.com" };
            var category = new Category
            {
                Id = id,
                Name = "Cat",
                IsDeleted = false,
                Products = new List<Product> { new Product() },
                Children = new List<Category>()
            };

            _claimsServiceMock.Setup(x => x.GetCurrentUserId).Returns(userId);
            _userServiceMock.Setup(x => x.GetUserDetailsByIdAsync(userId)).ReturnsAsync(userDto);
            _categoryRepoMock.Setup(x => x.GetQueryable()).Returns(new List<Category> { category }.AsQueryable());

            await Assert.ThrowsAsync<Exception>(() => _categoryService.DeleteAsync(id));
        }
    }
}