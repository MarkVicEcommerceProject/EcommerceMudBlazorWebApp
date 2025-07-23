using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public class CategoryService(ApplicationDbContext context) : ICategoryService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<IReadOnlyList<Category>> GetAllAsync()
        {
            return await _context.Categories.Include(c=>c.ParentCategory).ThenInclude(c=>c.ChildCategories).ToListAsync();
        }

        public async Task<Category> GetByIdAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id) ?? throw new KeyNotFoundException($"Category with Id {id} not found");
            return category;
        }

        public async Task<Category> CreateAsync(Category newCategory)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Categories.Add(newCategory);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return newCategory;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"[CategoryService] Error creating category: {ex.Message}");
                throw;
            }
        }

        public async Task<Category> UpdateAsync(int id, Category updatedCategory)
        {
            var existing = await _context.Categories.FindAsync(id) ?? throw new KeyNotFoundException($"Category with Id {id} not found");
            updatedCategory.Id = id;

            _context.Entry(existing).CurrentValues.SetValues(updatedCategory);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories
                .Include(c => c.ChildCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return false;

            if (category.ChildCategories.Any())
                throw new InvalidOperationException("Cannot delete a category with child categories.");

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<Category>> GetHierarchyAsync()
        {
            return await _context.Categories
                .Include(c => c.ChildCategories)
                .Where(c => c.ParentCategoryId == null)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Category>> GetChildrenAsync(int parentId)
        {
            return await _context.Categories
                .Where(c => c.ParentCategoryId == parentId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<int>> GetDescendantIdsAsync(int categoryId)
        {
            var result = new List<int>();
            var children = await GetChildrenAsync(categoryId);

            foreach (var child in children)
            {
                result.Add(child.Id);
                var descendants = await GetDescendantIdsAsync(child.Id);
                result.AddRange(descendants);
            }

            return result;
        }

        public async Task<Category> GetBySlugAsync(string slug)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Slug == slug) ?? throw new KeyNotFoundException($"Category with slug '{slug}' not found");
            return category;
        }

        public async Task AssignToProductAsync(int productId, int categoryId)
        {
            var exists = await _context.ProductCategories
                .AnyAsync(pc => pc.ProductId == productId && pc.CategoryId == categoryId);
            if (exists) return;

            var pc = new ProductCategory
            {
                ProductId = productId,
                CategoryId = categoryId
            };

            _context.ProductCategories.Add(pc);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFromProductAsync(int productId, int categoryId)
        {
            var pc = await _context.ProductCategories
                .FirstOrDefaultAsync(pc => pc.ProductId == productId && pc.CategoryId == categoryId);

            if (pc != null)
            {
                _context.ProductCategories.Remove(pc);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<Category>> GetByProductAsync(int productId)
        {
            return await _context.ProductCategories
                .Where(pc => pc.ProductId == productId)
                .Include(pc => pc.Category)
                .Select(pc => pc.Category)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetProductsByCategoryAsync(int categoryId, bool includeDescendants = true)
        {
            var categoryIds = new List<int> { categoryId };

            if (includeDescendants)
                categoryIds.AddRange(await GetDescendantIdsAsync(categoryId));

            return await _context.ProductCategories
                .Where(pc => categoryIds.Contains(pc.CategoryId))
                .Include(pc => pc.Product)
                .Select(pc => pc.Product)
                .Distinct()
                .ToListAsync();
        }
    }
}
