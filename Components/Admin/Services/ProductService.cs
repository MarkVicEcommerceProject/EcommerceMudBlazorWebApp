using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public class ProductService(ApplicationDbContext context) : IProductService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _context.Products.Include(p => p.ProductCategories).ToListAsync();
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            return await _context.Products.Include(p => p.ProductCategories)
                                          .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task CreateProductAsync(Product product)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var firstCategoryName = "Default";
                if (product.ProductCategories != null && product.ProductCategories.Count != 0)
                {
                    var firstCategoryId = product.ProductCategories.First().CategoryId;
                    var category = await _context.Categories.FindAsync(firstCategoryId);
                    firstCategoryName = category?.Name ?? "Default";
                }

                product.SKU = await GenerateSKUAsync(firstCategoryName, product.Name)
                    ?? throw new InvalidOperationException("Failed to generate SKU.");
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                // Detach the join-rows temporarily
                var categories = product.ProductCategories?.ToList() ?? new List<ProductCategory>();
                product.ProductCategories.Clear();

                // Insert the product
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Now insert each join-row with the real product.Id
                foreach (var pc in categories)
                {
                    _context.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = pc.CategoryId
                    });
                }
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateProductAsync(Product product, IEnumerable<int> newCategoryIds)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                product.UpdatedAt = DateTime.UtcNow;

                // 1) Update the product’s scalars
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                // 2) Refresh category assignments
                var existing = await _context.ProductCategories
                    .Where(pc => pc.ProductId == product.Id)
                    .ToListAsync();

                // Remove old
                _context.ProductCategories.RemoveRange(existing);

                // Add new
                foreach (var catId in newCategoryIds)
                {
                    _context.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = catId
                    });
                }
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }


        public async Task DeleteProductAsync(int id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await GetProductByIdAsync(id) ?? throw new KeyNotFoundException($"Product with ID {id} not found.");
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

        }
        
        public async Task<string> GenerateSKUAsync(string category, string name)
        {
            
            var sku = $"{category.Substring(0, 3).ToUpper()}-{name.Substring(0, 3).ToUpper()}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            return sku;
        }
        
    }
}