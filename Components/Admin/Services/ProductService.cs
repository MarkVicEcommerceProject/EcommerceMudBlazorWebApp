using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public class ProductService(IDbContextFactory<ApplicationDbContext> contextFactory) : IProductService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products
                                .Include(p => p.ProductCategories)
                                .ToListAsync();
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products
                                .Include(p => p.ProductCategories)
                                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task CreateProductAsync(Product product)
        {
            using var context = _contextFactory.CreateDbContext();
            using var tx = await context.Database.BeginTransactionAsync();

            try
            {
                var firstCategoryName = "Default";
                if (product.ProductCategories != null && product.ProductCategories.Count != 0)
                {
                    var firstCategoryId = product.ProductCategories.First().CategoryId;
                    var category = await context.Categories.FindAsync(firstCategoryId);
                    firstCategoryName = category?.Name ?? "Default";
                }

                product.SKU = await GenerateSKUAsync(firstCategoryName, product.Name)
                    ?? throw new InvalidOperationException("Failed to generate SKU.");
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                // Temporarily detach categories
                var categories = product.ProductCategories?.ToList() ?? new List<ProductCategory>();
                product.ProductCategories.Clear();

                context.Products.Add(product);
                await context.SaveChangesAsync();

                // Add join entries
                foreach (var pc in categories)
                {
                    context.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = pc.CategoryId
                    });
                }

                await context.SaveChangesAsync();
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
            using var context = _contextFactory.CreateDbContext();
            using var tx = await context.Database.BeginTransactionAsync();

            try
            {
                var existing = await context.Products
                                            .Include(p => p.ProductCategories)
                                            .FirstOrDefaultAsync(p => p.Id == product.Id)
                              ?? throw new Exception("Product not found");

                // Update fields
                context.Entry(existing).CurrentValues.SetValues(product);
                existing.UpdatedAt = DateTime.UtcNow;

                // Update categories
                context.ProductCategories.RemoveRange(existing.ProductCategories);

                foreach (var catId in newCategoryIds)
                {
                    context.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = catId
                    });
                }

                await context.SaveChangesAsync();
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
            using var context = _contextFactory.CreateDbContext();
            using var tx = await context.Database.BeginTransactionAsync();

            try
            {
                var product = await context.Products
                                           .Include(p => p.ProductCategories)
                                           .FirstOrDefaultAsync(p => p.Id == id)
                              ?? throw new KeyNotFoundException($"Product with ID {id} not found.");

                context.Products.Remove(product);
                await context.SaveChangesAsync();
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
            // This method doesn't require DbContext
            var sku = $"{category[..3].ToUpper()}-{name[..3].ToUpper()}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            return await Task.FromResult(sku);
        }
    }
}
