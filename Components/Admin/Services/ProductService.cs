using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using ECommerceMudblazorWebApp.Services.Recommendations;
using Microsoft.EntityFrameworkCore.Internal;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public class ProductService(IDbContextFactory<ApplicationDbContext> contextFactory,IRecommendationService recommendationService) : IProductService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory = contextFactory;
        private readonly IRecommendationService _recommendationService = recommendationService;

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
                                .FirstOrDefaultAsync(p => p.Id == id) ?? throw new KeyNotFoundException($"Product with ID {id} not found.");
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

        public async Task<bool> UpdateProductAsync(
            Product product,
            IEnumerable<int> newCategoryIds,
            IEnumerable<int>? newTagIds = null,
            bool setFeatured = false,
            DateTime? featuredStart = null,
            DateTime? featuredEnd = null,
            bool setDailyDeal = false,
            decimal? dailyDealPrice = null,
            DateTime? dailyDealStart = null,
            DateTime? dailyDealEnd = null,
            bool setFlashSale = false,
            decimal? flashSalePrice = null,
            DateTime? flashSaleStart = null,
            DateTime? flashSaleEnd = null)
                {
                    await using var db = _contextFactory.CreateDbContext();
                    await using var tx = await db.Database.BeginTransactionAsync();

                    try
                    {
                        var existing = await db.Products
                            .Include(p => p.ProductCategories)
                            .Include(p => p.ProductTags)
                            .FirstOrDefaultAsync(p => p.Id == product.Id);

                        if (existing == null) return false;

                        existing.Name = product.Name;
                        existing.Price = product.Price;
                        existing.Description = product.Description;
                        existing.StockQuantity = product.StockQuantity;
                        existing.ImagePath = product.ImagePath;
                        existing.ImageUrl = product.ImageUrl;
                        existing.IsActive = product.IsActive;
                        existing.UpdatedAt = DateTime.UtcNow;

                        // Denormalized flags that may be updated directly (we'll sync DB-level changes below)
                        existing.IsFeatured = setFeatured;
                        existing.IsDailyDeal = setDailyDeal;
                        existing.IsFlashSale = setFlashSale;

                        // If product-level daily/flash fields exist, update them too (denormalized)
                        existing.DailyDealPrice = dailyDealPrice;
                        if (flashSalePrice.HasValue)
                        {
                            existing.FlashSalePrice = flashSalePrice;
                        }
                        if (flashSaleStart.HasValue) existing.FlashSaleStart = flashSaleStart;
                        if (flashSaleEnd.HasValue) existing.FlashSaleEnd = flashSaleEnd;

                        // ---- categories: sync (delete removed, add new)
                        var newCatSet = newCategoryIds?.ToHashSet() ?? new HashSet<int>();
                        var toRemoveCats = existing.ProductCategories
                            .Where(pc => !newCatSet.Contains(pc.CategoryId))
                            .ToList();
                        if (toRemoveCats.Count != 0) db.ProductCategories.RemoveRange(toRemoveCats);

                        var existingCatIds = existing.ProductCategories.Select(pc => pc.CategoryId).ToHashSet();
                        var toAddCats = newCatSet.Except(existingCatIds).Select(cid => new ProductCategory { ProductId = existing.Id, CategoryId = cid });
                        if (toAddCats.Any()) db.ProductCategories.AddRange(toAddCats);

                        // ---- tags: sync (delete removed, add new)
                        var newTagSet = (newTagIds ?? Enumerable.Empty<int>()).ToHashSet();
                        var toRemoveTags = existing.ProductTags.Where(pt => !newTagSet.Contains(pt.TagId)).ToList();
                        if (toRemoveTags.Count != 0) db.ProductTags.RemoveRange(toRemoveTags);

                        var existingTagIds = existing.ProductTags.Select(pt => pt.TagId).ToHashSet();
                        var toAddTags = newTagSet.Except(existingTagIds).Select(tid => new ProductTag { ProductId = existing.Id, TagId = tid });
                        if (toAddTags.Any()) db.ProductTags.AddRange(toAddTags);

                        // Persist the product and link changes first
                        await db.SaveChangesAsync();

                        // ---- Now: update related admin tables via RecommendationService
                        // NOTE: these RecommendationService calls themselves open their own DB contexts in your current design.
                        // We call them after saving product to keep a clear transaction boundary.
                        // Featured
                        if (setFeatured)
                        {
                            await _recommendationService.SetProductFeaturedAsync(existing.Id, true, featuredStart, featuredEnd,0,externalDb: db);
                        }
                        else
                        {
                            // If admin cleared the featured flag, remove any featured rows
                            await _recommendationService.RemoveFeaturedProductAsync(existing.Id,externalDb: db);
                        }

                        // DailyDeal
                        if (setDailyDeal && dailyDealPrice.HasValue)
                        {
                            await _recommendationService.SetProductAsDailyDealAsync(existing.Id, dailyDealPrice, date: dailyDealStart?.Date ?? DateTime.UtcNow.Date,
                                priority: 0, startAt: dailyDealStart, endAt: dailyDealEnd,externalDb: db);
                        }
                        else
                        {
                            // If admin turned off daily deal, remove current day's daily deal or all daily deals (choose policy)
                            if (!setDailyDeal)
                                await _recommendationService.RemoveDailyDealAsync(existing.Id,null,externalDb:db); 
                        }

                        // Flash sale
                        if (setFlashSale && flashSalePrice.HasValue)
                        {
                            // pass the desired sale window if provided
                            await _recommendationService.AddOrUpdateFlashSaleItemAsync(null, existing.Id, flashSalePrice.Value, priority: 0, saleStart: flashSaleStart, saleEnd: flashSaleEnd,externalDb: db);
                        }
                        else
                        {
                            // remove from any flash sales if admin turned off IsFlashSale
                            if (!setFlashSale)
                                await _recommendationService.RemoveProductFromAnyFlashSaleAsync(existing.Id, externalDb: db);
                        }

                        await db.Entry(existing).ReloadAsync();
                        existing.UpdatedAt = DateTime.UtcNow;
                        db.Products.Update(existing);
                        await db.SaveChangesAsync();

                        await tx.CommitAsync();

                        return true;
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

        public async Task<List<Tag>> GetProductTagsAsync(int productId)
        {
            await using var db = _contextFactory.CreateDbContext();
            var tags = await db.ProductTags
                .Where(pt => pt.ProductId == productId)
                .Include(pt => pt.Tag)
                .Select(pt => pt.Tag)
                .AsNoTracking()
                .ToListAsync();
            return tags;
        }

        public async Task AddTagToProductAsync(int productId, int tagId)
        {
            await using var db = _contextFactory.CreateDbContext();
            // avoid duplicates
            var exists = await db.ProductTags.AnyAsync(pt => pt.ProductId == productId && pt.TagId == tagId);
            if (exists) return;

            db.ProductTags.Add(new ProductTag { ProductId = productId, TagId = tagId });
            await db.SaveChangesAsync();
        }

        public async Task RemoveTagFromProductAsync(int productId, int tagId)
        {
            await using var db = _contextFactory.CreateDbContext();
            var link = await db.ProductTags.FirstOrDefaultAsync(pt => pt.ProductId == productId && pt.TagId == tagId);
            if (link == null) return;
            db.ProductTags.Remove(link);
            await db.SaveChangesAsync();
        }

        public async Task<IEnumerable<InventoryAlert>> GetInventoryAlertsAsync(int threshold = 10)
        {
            await using var db = _contextFactory.CreateDbContext();

            var list = await db.Products
                .Where(p => p.StockQuantity <= threshold)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new InventoryAlert
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    StockLeft = p.StockQuantity
                })
                .ToListAsync();

            return list;
        }
        public async Task RecordProductViewAsync(int productId, string? userId = null, string? guestId = null)
        {
            await using var _db = _contextFactory.CreateDbContext();

            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-5);
            var today = now.Date;

            // ---------------------------------------------
            // Registered user - detailed logging + stats
            // ---------------------------------------------
            if (!string.IsNullOrEmpty(userId))
            {
                // ---- Throttle: only one view per user/product within 5 minutes
                bool recentlyViewed = await _db.ProductViews
                    .AsNoTracking()
                    .AnyAsync(v => v.ProductId == productId &&
                                   v.UserId == userId &&
                                   v.ViewedAt >= cutoff);

                if (recentlyViewed) return;

                // ---- Insert detailed view row
                _db.ProductViews.Add(new ProductView
                {
                    ProductId = productId,
                    UserId = userId,
                    ViewedAt = now
                });

                // ---- Increment product counter
                await _db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(p => p.ViewsCount, p => p.ViewsCount + 1));

                // ---- Update or insert daily stat
                var rows = await _db.ProductDailyStat
                    .Where(s => s.ProductId == productId && s.Date == today)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(s => s.Views, s => s.Views + 1));

                if (rows == 0)
                {
                    _db.ProductDailyStat.Add(new ProductDailyStat
                    {
                        ProductId = productId,
                        Date = today,
                        Views = 1,
                        Sales = 0
                    });
                }

                await _db.SaveChangesAsync();
                return;
            }

            // ---------------------------------------------
            // Guest user - only counters, no detailed logging
            // ---------------------------------------------
            if (!string.IsNullOrEmpty(guestId))
            {

                await _db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(p => p.ViewsCount, p => p.ViewsCount + 1));

                var rows = await _db.ProductDailyStat
                    .Where(s => s.ProductId == productId && s.Date == today)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(s => s.Views, s => s.Views + 1));

                if (rows == 0)
                {
                    _db.ProductDailyStat.Add(new ProductDailyStat
                    {
                        ProductId = productId,
                        Date = today,
                        Views = 1,
                        Sales = 0
                    });
                }

                await _db.SaveChangesAsync();
                return;
            }

        }

        //Helpers
        public async Task<string> GenerateSKUAsync(string category, string name)
        {
            var sku = $"{category[..3].ToUpper()}-{name[..3].ToUpper()}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            return await Task.FromResult(sku);
        }

        
    }
}
