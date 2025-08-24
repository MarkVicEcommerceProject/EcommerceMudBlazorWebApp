using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceMudblazorWebApp.Services.Recommendations
{
    public class RecommendationService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IMemoryCache cache, ILogger<RecommendationService> logger) : IRecommendationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory;
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<RecommendationService> _logger = logger;

        #region -- Cache Helpers
        private const string CACHE_VERSION_KEY = "recommendation_version";

        private string GetCacheVersion()
        {
            if (_cache.TryGetValue<string>(CACHE_VERSION_KEY, out var token) && !string.IsNullOrEmpty(token))
                return token;

            token = Guid.NewGuid().ToString("N");
            _cache.Set(CACHE_VERSION_KEY, token);
            return token;
        }

        private string VersionedKey(string baseKey)
        {
            var version = GetCacheVersion();
            return $"{baseKey}:v{version}";
        }

        private void BumpRecommendationCacheVersion()
        {
            _cache.Set(CACHE_VERSION_KEY, Guid.NewGuid().ToString("N"));
        }

        private void RemoveKey(string key)
        {
            _cache.Remove(key);
        }
        #endregion


        // 1. Flash sales - supports both simple product flags and FlashSale/FlashSaleItem model
        public async Task<IEnumerable<Product>> GetFlashSalesAsync(DateTime? referenceDate = null, int limit = 20)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var now = referenceDate ?? DateTime.UtcNow;

            var cacheKey = VersionedKey($"flashsales:{now:yyyyMMddHHmm}:{limit}");
            if (_cache.TryGetValue(cacheKey, out List<Product> cached))
                return cached;

            // Try to fetch active flash sale items for the given date (prefer items scoped to a flash sale)
            var items = await _db.FlashSaleItems
                .Include(fi => fi.FlashSale)
                .Include(fi => fi.Product) // include product to reduce later queries
                .Where(fi => fi.FlashSale.StartAt <= now && fi.FlashSale.EndAt >= now
                             && fi.Product.IsActive && fi.Product.StockQuantity > 0)
                .AsNoTracking()
                .ToListAsync();

            if (items != null && items.Count > 0)
            {
                // If same product appears multiple times (multiple sales), pick the best item per product:
                // choose lowest Priority, then lowest SalePrice (or highest discount)
                var bestPerProduct = items
                    .GroupBy(fi => fi.ProductId)
                    .Select(g =>
                    {
                        var best = g
                            .OrderBy(fi => fi.Priority)
                            .ThenBy(fi => fi.SalePrice)
                            .First();
                        return new { best.Product, best.SalePrice, best.Priority };
                    })
                    // Rank by priority then by effective discount (bigger discount first)
                    .OrderBy(x => x.Priority)
                    .ThenByDescending(x => (x.Product.Price - (x.SalePrice == 0 ? x.Product.Price : x.SalePrice)))
                    .Take(limit)
                    .Select(x => x.Product)
                    .ToList();
                _cache.Set(cacheKey, bestPerProduct, TimeSpan.FromMinutes(5));
                return bestPerProduct;
            }

            // Fallback
            var fallback = await _db.Products
                .Where(p => p.IsActive && p.IsFlashSale
                            && p.FlashSaleStart.HasValue && p.FlashSaleEnd.HasValue
                            && p.FlashSaleStart <= now && p.FlashSaleEnd >= now
                            && p.StockQuantity > 0)
                .OrderByDescending(p => (p.Price - (p.FlashSalePrice ?? p.Price))) // biggest discount first
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
            _cache.Set(cacheKey, fallback, TimeSpan.FromMinutes(5));
            return fallback;
        }

        // 2. Daily deals
        public async Task<IEnumerable<DailyDealDto>> GetDailyDealsAsync(DateTime date, int limit = 5)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var day = date.Date;

            var cacheKey = VersionedKey($"dailydeals:{day:yyyyMMdd}:{limit}");
            if (_cache.TryGetValue(cacheKey, out List<DailyDealDto> cached))
                return cached;

            if (await _db.Set<DailyDeal>().AnyAsync())
            {
                var deals = await _db.DailyDeals
                    .Include(d => d.Product)
                    .Where(d => d.Date == day && d.Product.IsActive && d.Product.StockQuantity > 0)
                    .OrderBy(d => d.Priority)
                    .Take(limit)
                    .AsNoTracking()
                    .ToListAsync();

                var results = deals.Select(d => new DailyDealDto
                {
                    ProductId = d.ProductId,
                    Product = d.Product,
                    DealPrice = d.DealPrice ?? d.Product.Price,
                    Date = d.Date,
                    StartAt = d.StartAt ?? d.Date,
                    EndAt = d.EndAt ?? d.Date.AddDays(1).AddSeconds(-1),
                    Priority = d.Priority
                }).ToList();

                _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));
                return results;
            }

            // Fallback
            // NOTE: since Product has no per-day DailyDealStart/End fields in the model, fallback will not have exact timing
            var fallbackProducts = await _db.Products
                .Where(p => p.IsActive && p.IsDailyDeal && p.StockQuantity > 0)
                .OrderByDescending(p => p.TotalSalesCount)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();

            var fallbackResults = fallbackProducts.Select(p => new DailyDealDto
            {
                ProductId = p.Id,
                Product = p,
                DealPrice = p.DailyDealPrice?? (Math.Ceiling(0.8m * p.Price)), 
                Date = day,
                StartAt = day,
                EndAt = day.AddDays(1).AddSeconds(-1),
                Priority = 0
            }).ToList();

            _cache.Set(cacheKey, fallbackResults, TimeSpan.FromMinutes(3));
            return fallbackResults;
        }


        // 3. Trending Now - aggregated through ProductDailyStat (fast)
        public async Task<IEnumerable<Product>> GetTrendingNowAsync(DateTime from, DateTime to, int limit = 20)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            
            var cacheKey = VersionedKey($"trending:{from:yyyyMMdd}:{to:yyyyMMdd}:{limit}");
            if (_cache.TryGetValue(cacheKey, out List<Product> cached)) return cached;

            // Weighted score: give sales higher weight than views
            var scores = await _db.ProductDailyStat
                .Where(s => s.Date >= from.Date && s.Date <= to.Date)
                .GroupBy(s => s.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Score = g.Sum(x => x.Sales * 5 + x.Views) // adjustable weights
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToListAsync();

            if (scores.Count == 0) return Enumerable.Empty<Product>();

            var productIds = scores.Select(x => x.ProductId).ToList();

            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var productDict = products.ToDictionary(p => p.Id);

            var ordered = scores
                .Where(s => productDict.ContainsKey(s.ProductId))
                .Select(s => productDict[s.ProductId])
                .ToList();

            _cache.Set(cacheKey, ordered, TimeSpan.FromMinutes(5));
            return ordered;
        }

        // 4. New arrivals
        public async Task<IEnumerable<Product>> GetNewArrivalsAsync(TimeSpan window, int limit = 20)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var cutoff = DateTime.UtcNow - window;
            return await _db.Products
                .Where(p => p.IsActive && p.CreatedAt >= cutoff)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        // 5. Related Products (categories + tags)
        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int limit = 10)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            // get categories and tags of the product
            var categoryIds = await _db.ProductCategories
                .Where(pc => pc.ProductId == productId)
                .Select(pc => pc.CategoryId)
                .ToListAsync();

            var tagIds = await _db.ProductTags
                .Where(pt => pt.ProductId == productId)
                .Select(pt => pt.TagId)
                .ToListAsync();

            // Score products by category match (weight 3) and tag overlap (weight 1), and prefer similar price proximity
            var query = _db.Products
                .Where(p => p.Id != productId && p.IsActive)
                .Select(p => new
                {
                    Product = p,
                    CategoryMatch = p.ProductCategories.Count(pc => categoryIds.Contains(pc.CategoryId)),
                    TagMatch = p.ProductTags.Count(pt => tagIds.Contains(pt.TagId))
                })
                .Where(x => x.CategoryMatch > 0 || x.TagMatch > 0)
                .OrderByDescending(x => x.CategoryMatch * 3 + x.TagMatch)
                .ThenByDescending(x => x.Product.TotalSalesCount) // tie-breaker: popularity
                .Take(limit)
                .AsNoTracking();

            var results = await query.ToListAsync();
            return results.Select(r => r.Product).ToList();
        }

        // 6. You may also like - on-the-fly co-purchase (works without precomputed associations)
        public async Task<IEnumerable<Product>> GetYouMayAlsoLikeAsync(
            string? userId, List<int> cartProductIds, int limit = 12, ApplicationDbContext? externalDb = null)
                {
                    var _db = externalDb ?? _dbContextFactory.CreateDbContext();
                    var dispose = externalDb == null;

                    try
                    {
                        if (cartProductIds == null || cartProductIds.Count == 0)
                            return [];

                // Cache by sorted cart ids to reduce load, keyed short time
                        var key = VersionedKey($"also:{string.Join('-', cartProductIds.OrderBy(i => i))}:{limit}");
                        if (_cache.TryGetValue(key, out List<Product> cached))
                            return cached;

                        // Find orders that contain any cart product, then find other products in those orders
                        var assoc = await _db.OrderItems
                            .Where(oi => cartProductIds.Contains(oi.ProductId))
                            .Select(oi => oi.OrderId)
                            .Distinct()
                            .Join(_db.OrderItems, oid => oid, oi => oi.OrderId, (oid, oi) => new { oi.ProductId })
                            .Where(x => !cartProductIds.Contains(x.ProductId))
                            .GroupBy(x => x.ProductId)
                            .Select(g => new { ProductId = g.Key, Count = g.Count() })
                            .OrderByDescending(g => g.Count)
                            .Take(limit * 3) // fetch a few more then filter by stock/active
                            .ToListAsync();

                        var productIds = assoc.Select(a => a.ProductId).ToList();

                        var products = await _db.Products
                            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.StockQuantity > 0)
                            .AsNoTracking()
                            .ToListAsync();

                        // Create dictionary for O(1) lookup
                        var productDict = products.ToDictionary(p => p.Id, p => p);

                        // Preserve assoc order and filter by products that exist in dict
                        var ordered = productIds
                            .Where(id => productDict.ContainsKey(id))
                            .Select(id => productDict[id])
                            .Take(limit)
                            .ToList();

                        _cache.Set(key, ordered, TimeSpan.FromMinutes(5));
                        return ordered;
                    }
                    finally
                    {
                        if (dispose) await _db.DisposeAsync();
                    }
        }


        // 7. Suggestions after purchase (based on order contents - use same logic as above)
        public async Task<IEnumerable<Product>> GetSuggestionsAfterPurchaseAsync(int orderId, int limit = 12)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return [];

            var boughtIds = order.OrderItems.Select(oi => oi.ProductId).ToList();
            return await GetYouMayAlsoLikeAsync(order.UserId, boughtIds, limit, _db);
        }

        // 8. Recently bought by user
        public async Task<IEnumerable<Product>> GetRecentlyBoughtByUserAsync(string? userId, int limit = 20)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            if (string.IsNullOrEmpty(userId)) return [];

            var productIds = await _db.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .SelectMany(o => o.OrderItems.Select(oi => new { oi.ProductId, o.OrderDate }))
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, LastBought = g.Max(x => x.OrderDate) })
                .OrderByDescending(x => x.LastBought)
                .Take(limit)
                .Select(x => x.ProductId)
                .ToListAsync();

            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .AsNoTracking()
                .ToListAsync();

            var ordered = productIds.Select(id => products.First(p => p.Id == id)).ToList();
            return ordered;
        }

        // 9. Featured products (admin curated)
        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int limit = 20)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var cacheKey = VersionedKey($"featured:list:{limit}");
            if (_cache.TryGetValue(cacheKey, out List<Product> cached)) return cached;

            if (_db.Set<FeaturedProduct>().Any())
            {
                var now = DateTime.UtcNow;
                var featured = await _db.FeaturedProducts
                    .Where(f => (f.StartDate == null || f.StartDate <= now) && (f.EndDate == null || f.EndDate >= now))
                    .OrderBy(f => f.Position)
                    .Take(limit)
                    .Select(f => f.Product)
                    .AsNoTracking()
                    .ToListAsync();
                _cache.Set(cacheKey, featured,TimeSpan.FromMinutes(10));
                return featured;
            }

            return await _db.Products
                .Where(p => p.IsActive && p.IsFeatured)
                .OrderByDescending(p => p.TotalSalesCount)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task SetProductFeaturedAsync(int productId,bool isFeatured,DateTime? startDate = null,DateTime? endDate = null,int position = 0,ApplicationDbContext? externalDb = null)
        {
            var db = externalDb ?? _dbContextFactory.CreateDbContext();
            var ownContext = externalDb == null;
            IDbContextTransaction? tx = null;

            try
            {
                if (ownContext)
                    tx = await db.Database.BeginTransactionAsync();

                var fp = await db.FeaturedProducts.FirstOrDefaultAsync(f => f.ProductId == productId);

                if (isFeatured)
                {
                    if (fp == null)
                    {
                        fp = new FeaturedProduct
                        {
                            ProductId = productId,
                            Position = position,
                            StartDate = startDate,
                            EndDate = endDate
                        };
                        db.FeaturedProducts.Add(fp);
                    }
                    else
                    {
                        fp.Position = position;
                        fp.StartDate = startDate;
                        fp.EndDate = endDate;
                        db.FeaturedProducts.Update(fp);
                    }

                    if (ownContext)
                    {
                        // fast set-based update when running standalone
                        await db.Products
                            .Where(p => p.Id == productId)
                            .ExecuteUpdateAsync(u => u.SetProperty(p => p.IsFeatured, p => true));
                    }
                    else
                    {
                        // when using externalDb, set tracked product flag instead of ExecuteUpdateAsync
                        var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                        if (prod != null) prod.IsFeatured = true;
                    }
                }
                else
                {
                    if (fp != null)
                        db.FeaturedProducts.Remove(fp);

                    if (ownContext)
                    {
                        await db.Products
                            .Where(p => p.Id == productId)
                            .ExecuteUpdateAsync(u => u.SetProperty(p => p.IsFeatured, p => false));
                    }
                    else
                    {
                        var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                        if (prod != null) prod.IsFeatured = false;
                    }
                }

                if (ownContext)
                {
                    await db.SaveChangesAsync();
                    await tx!.CommitAsync();

                    BumpRecommendationCacheVersion();
                    RemoveKey(VersionedKey($"featured:list"));
                    RemoveKey(VersionedKey($"product:{productId}:details"));

                    await tx.DisposeAsync();
                }
                else
                {
                    // No SaveChanges here - caller will persist everything
                    // still do cache invalidation later after outer commit if desired
                    Console.WriteLine("product is set as featured");
                }
            }
            catch
            {
                if (tx != null) 
                {   
                    await tx.RollbackAsync(); 
                    await tx.DisposeAsync(); 
                }
                throw;
            }
            finally
            {
                if (ownContext) await db.DisposeAsync();
            }
        }


        public async Task RemoveFeaturedProductAsync(int productId, ApplicationDbContext? externalDb = null)
        {
            await SetProductFeaturedAsync(productId, false, null, null,0,externalDb);
        }
        public async Task AddOrUpdateFlashSaleItemAsync(int? flashSaleId,int productId,decimal salePrice,int priority = 0,DateTime? saleStart = null,DateTime? saleEnd = null,string? saleName = null,ApplicationDbContext? externalDb = null)
        {
            var db = externalDb ?? _dbContextFactory.CreateDbContext();
            var ownContext = externalDb == null;
            IDbContextTransaction? tx = null;

            try
            {
                if (ownContext) tx = await db.Database.BeginTransactionAsync();

                FlashSale sale = null;

                if (flashSaleId.HasValue)
                {
                    sale = await db.FlashSales.Include(fs => fs.Items).FirstOrDefaultAsync(fs => fs.Id == flashSaleId.Value)
                        ?? throw new InvalidOperationException("FlashSale not found");
                }
                else if (saleStart.HasValue && saleEnd.HasValue)
                {
                    var start = saleStart.Value;
                    var end = saleEnd.Value;
                    sale = await db.FlashSales.Include(fs => fs.Items)
                        .FirstOrDefaultAsync(fs => fs.StartAt == start && fs.EndAt == end);

                    if (sale == null)
                    {
                        sale = new FlashSale
                        {
                            Name = string.IsNullOrWhiteSpace(saleName) ? $"Flash Sale ({start:yyyy-MM-dd} – {end:yyyy-MM-dd})" : saleName,
                            StartAt = start,
                            EndAt = end
                        };
                        db.FlashSales.Add(sale);

                        if (ownContext)
                        {
                            await db.SaveChangesAsync(); // ensure sale.Id exists
                        }
                    }
                }
                else
                {
                    var now = DateTime.UtcNow;
                    sale = await db.FlashSales.Include(fs => fs.Items)
                        .FirstOrDefaultAsync(fs => fs.StartAt <= now && fs.EndAt >= now);

                    if (sale == null)
                    {
                        sale = new FlashSale
                        {
                            Name = $"Today's Flash Sale ({now:yyyy-MM-dd})",
                            StartAt = now.Date,
                            EndAt = now.Date.AddDays(1).AddSeconds(-1)
                        };
                        db.FlashSales.Add(sale);
                        if (ownContext) await db.SaveChangesAsync();
                    }
                }

                // If we created sale in externalDb mode and didn't SaveChanges, sale is tracked and will receive Id on SaveChanges.
                
                var saleId = sale != null ? sale.Id : 0;
                var item = await db.FlashSaleItems.FirstOrDefaultAsync(i => i.FlashSaleId == saleId && i.ProductId == productId);
                if (item == null)
                {
                    // If sale.Id == 0 because sale was newly added and not saved, create item using navigation property
                    item = new FlashSaleItem
                    {
                        FlashSale = sale,
                        ProductId = productId,
                        SalePrice = salePrice,
                        Priority = priority
                    };
                    db.FlashSaleItems.Add(item);
                }
                else
                {
                    item.SalePrice = salePrice;
                    item.Priority = priority;
                    db.FlashSaleItems.Update(item);
                }

                if (ownContext)
                {
                    await db.Products
                        .Where(p => p.Id == productId)
                        .ExecuteUpdateAsync(u => u
                            .SetProperty(p => p.IsFlashSale, p => true)
                            .SetProperty(p => p.FlashSalePrice, p => salePrice)
                            .SetProperty(p => p.FlashSaleStart, p => sale.StartAt)
                            .SetProperty(p => p.FlashSaleEnd, p => sale.EndAt));
                    await db.SaveChangesAsync();
                    await tx!.CommitAsync();
                    await tx.DisposeAsync();

                    BumpRecommendationCacheVersion();
                    RemoveKey(VersionedKey($"product:{productId}:details"));
                    RemoveKey(VersionedKey($"product:{productId}:recommendations"));
                    RemoveKey(VersionedKey("flashsales:list"));
                }
                else
                {
                    // When externalDb provided, just update tracked product flag instead of ExecuteUpdate
                    var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                    if (prod != null)
                    {
                        prod.IsFlashSale = true;
                        prod.FlashSalePrice = salePrice;
                        prod.FlashSaleStart = sale.StartAt;
                        prod.FlashSaleEnd = sale.EndAt;
                    }
                    Console.WriteLine("add or update flash sale called ");
                    // No SaveChanges here; caller will persist everything
                }
            }
            catch
            {
                if (tx != null) { await tx.RollbackAsync(); await tx.DisposeAsync(); }
                throw;
            }
            finally
            {
                if (ownContext) await db.DisposeAsync();
            }
        }


        public async Task RemoveProductFromAnyFlashSaleAsync(int productId, ApplicationDbContext? externalDb = null)
        {
            var db = externalDb ?? _dbContextFactory.CreateDbContext();
            var ownContext = externalDb == null;
            IDbContextTransaction? tx = null;

            try
            {
                if (ownContext) tx = await db.Database.BeginTransactionAsync();

                var items = db.FlashSaleItems.Where(i => i.ProductId == productId);

                if (ownContext)
                {
                    db.FlashSaleItems.RemoveRange(items);
                    // Clear product-level fields via ExecuteUpdate
                    var existsOther = await db.FlashSaleItems.AnyAsync(i => i.ProductId == productId);
                    if (!existsOther)
                    {
                        await db.Products
                            .Where(p => p.Id == productId)
                            .ExecuteUpdateAsync(u => u
                                .SetProperty(p => p.IsFlashSale, p => false)
                                .SetProperty(p => p.FlashSalePrice, p => (decimal?)null)
                                .SetProperty(p => p.FlashSaleStart, p => (DateTime?)null)
                                .SetProperty(p => p.FlashSaleEnd, p => (DateTime?)null));
                    }
                    await db.SaveChangesAsync();
                    await tx!.CommitAsync();
                    await tx.DisposeAsync();

                    BumpRecommendationCacheVersion();
                    RemoveKey(VersionedKey($"product:{productId}:details"));
                    RemoveKey(VersionedKey("flashsales:list"));
                }
                else
                {
                    // externalDb: remove tracked items; don't SaveChanges
                    var toRemove = await items.ToListAsync();
                    if (toRemove.Any()) db.FlashSaleItems.RemoveRange(toRemove);

                    var hasOther = await db.FlashSaleItems.AnyAsync(i => i.ProductId == productId);
                    if (!hasOther)
                    {
                        var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                        if (prod != null)
                        {
                            prod.IsFlashSale = false;
                            prod.FlashSalePrice = null;
                            prod.FlashSaleStart = null;
                            prod.FlashSaleEnd = null;
                        }
                    }
                    // no SaveChanges here
                }
            }
            catch
            {
                if (tx != null) { await tx.RollbackAsync(); await tx.DisposeAsync(); }
                throw;
            }
            finally
            {
                if (ownContext) await db.DisposeAsync();
            }
        }



        public Task SyncProductFlashFieldsFromFlashSaleAsync(int productId)
        {
            throw new NotImplementedException();
        }

        public async Task<DailyDeal> SetProductAsDailyDealAsync(int productId,decimal? dealPrice,DateTime? date = null,int priority = 0,DateTime? startAt = null,DateTime? endAt = null,ApplicationDbContext? externalDb = null)
        {
            var db = externalDb ?? _dbContextFactory.CreateDbContext();
            var ownContext = externalDb == null;
            IDbContextTransaction? tx = null;

            try
            {
                if (ownContext) tx = await db.Database.BeginTransactionAsync();

                var targetDate = (date ?? DateTime.UtcNow).Date;
                var saleStart = startAt?.ToUniversalTime() ?? targetDate;
                var saleEnd = endAt?.ToUniversalTime() ?? targetDate.AddDays(1).AddSeconds(-1);

                var existing = await db.DailyDeals.FirstOrDefaultAsync(d => d.ProductId == productId && d.Date == targetDate);

                if (existing == null)
                {
                    existing = new DailyDeal
                    {
                        ProductId = productId,
                        Date = targetDate,
                        Priority = priority,
                        DealPrice = dealPrice,
                        StartAt = saleStart,
                        EndAt = saleEnd
                    };
                    db.DailyDeals.Add(existing);
                }
                else
                {
                    existing.Priority = priority;
                    existing.DealPrice = dealPrice;
                    existing.StartAt = saleStart;
                    existing.EndAt = saleEnd;
                    db.DailyDeals.Update(existing);
                }

                if (ownContext)
                {
                    await db.Products.Where(p => p.Id == productId)
                        .ExecuteUpdateAsync(up => up.SetProperty(p => p.IsDailyDeal, p => true));
                    await db.SaveChangesAsync();
                    await tx!.CommitAsync();
                    await tx.DisposeAsync();

                    BumpRecommendationCacheVersion();
                    RemoveKey(VersionedKey($"dailydeals:{targetDate:yyyyMMdd}"));
                    RemoveKey(VersionedKey($"product:{productId}:details"));
                    RemoveKey(VersionedKey($"product:{productId}:recommendations"));
                }
                else
                {
                    var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                    if (prod != null) prod.IsDailyDeal = true;
                    Console.WriteLine("setDailydeal has been executed");
                    // caller will SaveChanges
                }

                return existing;
            }
            catch
            {
                if (tx != null) { await tx.RollbackAsync(); await tx.DisposeAsync(); }
                throw;
            }
            finally
            {
                if (ownContext) await db.DisposeAsync();
            }
        }

        public async Task RemoveDailyDealAsync(int productId, DateTime? date = null, ApplicationDbContext? externalDb = null)
        {
            var db = externalDb ?? _dbContextFactory.CreateDbContext();
            var ownContext = externalDb == null;
            IDbContextTransaction? tx = null;

            try
            {
                if (ownContext) tx = await db.Database.BeginTransactionAsync();

                var targetDate = (date ?? DateTime.UtcNow).Date;

                var item = await db.DailyDeals.FirstOrDefaultAsync(d => d.ProductId == productId && d.Date == targetDate);
                if (item != null)
                    db.DailyDeals.Remove(item);

                if (ownContext)
                {
                    await db.SaveChangesAsync();
                    var hasOther = await db.DailyDeals.AnyAsync(d => d.ProductId == productId);
                    if (!hasOther)
                    {
                        await db.Products
                            .Where(p => p.Id == productId)
                            .ExecuteUpdateAsync(u => u.SetProperty(p => p.IsDailyDeal, p => false));
                    }

                    await tx!.CommitAsync();
                    await tx.DisposeAsync();

                    BumpRecommendationCacheVersion();
                    RemoveKey(VersionedKey($"dailydeals:{targetDate:yyyyMMdd}"));
                    RemoveKey(VersionedKey($"product:{productId}:details"));
                }
                else
                {
                    // caller will SaveChanges. Ensure product flag updated if no other daily deals remain:
                    var hasOther = await db.DailyDeals.AnyAsync(d => d.ProductId == productId);
                    if (!hasOther)
                    {
                        var prod = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                        if (prod != null) prod.IsDailyDeal = false;
                    }
                }
            }
            catch
            {
                if (tx != null) { await tx.RollbackAsync(); await tx.DisposeAsync(); }
                throw;
            }
            finally
            {
                if (ownContext) await db.DisposeAsync();
            }
        }

    }

}
