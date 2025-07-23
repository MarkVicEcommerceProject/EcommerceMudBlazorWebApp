using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public interface ICategoryService
    {
        // Basic CRUD
        Task<IReadOnlyList<Category>> GetAllAsync();
        Task<Category> GetByIdAsync(int id);
        Task<Category> CreateAsync(Category newCategory);
        Task<Category> UpdateAsync(int id, Category updatedCategory);
        Task<bool> DeleteAsync(int id);

        // Hierarchy
        Task<IReadOnlyList<Category>> GetHierarchyAsync();                // full tree
        Task<IReadOnlyList<Category>> GetChildrenAsync(int parentId);     // one level
        Task<IReadOnlyList<int>> GetDescendantIdsAsync(int categoryId);

        // Slug-based lookup
        Task<Category> GetBySlugAsync(string slug);

        // Product assignments
        Task AssignToProductAsync(int productId, int categoryId);
        Task RemoveFromProductAsync(int productId, int categoryId);
        Task<IReadOnlyList<Category>> GetByProductAsync(int productId);

        // Filtering
        Task<IReadOnlyList<Product>> GetProductsByCategoryAsync(int categoryId, bool includeDescendants = true);

    }

}