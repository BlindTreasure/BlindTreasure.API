using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Utils;

public interface ICategoryHelper
{
    Task<List<Guid>> GetAllChildCategoryIdsAsync(Guid parentCategoryId);
}

public class CategoryHelper : ICategoryHelper
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryHelper(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Guid>> GetAllChildCategoryIdsAsync(Guid parentCategoryId)
    {
        var allCategories = await _unitOfWork.Categories.GetQueryable()
            .Where(c => !c.IsDeleted)
            .ToListAsync();

        var result = new List<Guid> { parentCategoryId };

        void Traverse(Guid parentId)
        {
            var children = allCategories.Where(c => c.ParentId == parentId).ToList();
            foreach (var child in children)
            {
                result.Add(child.Id);
                Traverse(child.Id);
            }
        }

        Traverse(parentCategoryId);
        return result;
    }
}