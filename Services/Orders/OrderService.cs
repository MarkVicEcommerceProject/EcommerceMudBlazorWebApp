namespace ECommerceMudblazorWebApp.Services.Orders
{
    using ECommerceMudblazorWebApp.Data;
    using ECommerceMudblazorWebApp.Models;
    using Microsoft.EntityFrameworkCore;

    public class EfOrderService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IOrderService
    {
        public event Action? OnChange;
        public Order? CurrentOrder { get; set; }
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory;

        public async Task<int> PlaceOrderAsync(Order order)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.OrderDate = DateTime.UtcNow;
                order.Status = OrderStatus.PENDING;

                var random = new Random(order.Id);
                int days = random.Next(3, 6);
                order.DeliveredDate = order.OrderDate.AddDays(days);

                _db.Orders.Add(order);

                await _db.SaveChangesAsync();

                // --- Sales + Stats Update ---
                var today = DateTime.UtcNow.Date;

                // 1. Group items by product (avoid duplicate lookups if order has multiple same products)
                var groupedItems = order.OrderItems
                    .GroupBy(i => i.ProductId)
                    .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                    .ToList();

                // 2. Fetch all affected products in one query
                var productIds = groupedItems.Select(g => g.ProductId).ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                // 3. Fetch existing daily stats in one query
                var stats = await _db.ProductDailyStat
                    .Where(s => productIds.Contains(s.ProductId) && s.Date == today)
                    .ToListAsync();

                foreach (var group in groupedItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == group.ProductId);
                    if (product != null)
                    {
                        product.TotalSalesCount += group.Quantity;
                        product.StockQuantity -= group.Quantity;

                        // update stat
                        var stat = stats.FirstOrDefault(s => s.ProductId == group.ProductId);
                        if (stat == null)
                        {
                            stat = new ProductDailyStat
                            {
                                ProductId = product.Id,
                                Date = today,
                                Sales = group.Quantity,
                                Views = 0
                            };
                            _db.ProductDailyStat.Add(stat);
                        }
                        else
                        {
                            stat.Sales += group.Quantity;
                        }
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return order.Id;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<Order> GetOrderByIdAsync(int orderId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new KeyNotFoundException($"No orders found for user ID {userId}.");
        }

        public async Task CancelOrderAsync(int orderId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");

            if (order.Status == OrderStatus.CANCELLED)
                return;

            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockQuantity += item.Quantity;
                        _db.Entry(item.Product).Property(p => p.StockQuantity).IsModified = true;
                        _db.Products.Update(item.Product);
                    }
                }

                order.Status = OrderStatus.CANCELLED;
                _db.Orders.Update(order);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                OnChange?.Invoke();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<Order?> GetOrderDetailsAsync(int orderId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new InvalidOperationException("No orders found.");
        }

        public async Task<bool> UpdateOrderAsync(Order updatedOrder)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == updatedOrder.Id) ?? throw new KeyNotFoundException($"Order with ID {updatedOrder.Id} not found.");
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.ShippingAddress = updatedOrder.ShippingAddress;
                order.PaymentMethod = updatedOrder.PaymentMethod;

                var prevStatus = order.Status;
                order.Status = updatedOrder.Status;

                if(order.Status == OrderStatus.DELIVERED)
                    order.DeliveredDate = DateTime.UtcNow;

                if (prevStatus == OrderStatus.DELIVERED && order.Status != OrderStatus.DELIVERED)
                    {
                        order.DeliveredDate = DateTime.MinValue;
                    }

                _db.Orders.Update(order);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                OnChange?.Invoke();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await GetOrderByIdAsync(orderId) ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
            order.Status = newStatus;
            return await UpdateOrderAsync(order);
        }

        public async Task<OrdersAnalytics> GetOrdersAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            await using var db = _dbContextFactory.CreateDbContext();

            var s = startDate.Date;
            var e = endDate.Date;
            if (e < s) (s, e) = (e, s);

            int lengthInclusive = (e - s).Days + 1;
            var previousStart = s.AddDays(-lengthInclusive);
            var previousEndExclusive = s;
            var currentEndExclusive = e.AddDays(1);

            var ordersGrouped = await db.Orders
                .AsNoTracking()
                .Where(o =>
                    (o.OrderDate >= previousStart && o.OrderDate < previousEndExclusive) ||
                    (o.OrderDate >= s && o.OrderDate < currentEndExclusive))
                .Select(o => new
                {
                    Period = o.OrderDate >= s && o.OrderDate < currentEndExclusive ? 0 : 1,
                    Revenue = o.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice),
                    IsPending = (o.Status == OrderStatus.PENDING || o.Status == OrderStatus.PROCESSING) ? 1 : 0,
                    IsReturned = (o.Status == OrderStatus.RETURNED) ? 1 : 0
                })
                .GroupBy(x => x.Period)
                .Select(g => new
                {
                    Period = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    Count = g.Count(),
                    Pending = g.Sum(x => x.IsPending),
                    Returned = g.Sum(x => x.IsReturned)
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var fulfilledGrouped = await db.Orders
                .AsNoTracking()
                .Where(o =>
                    o.DeliveredDate != null &&
                    (o.Status == OrderStatus.DELIVERED || o.Status == OrderStatus.SHIPPED) &&
                    (
                        (o.DeliveredDate >= previousStart && o.DeliveredDate < previousEndExclusive) ||
                        (o.DeliveredDate >= s && o.DeliveredDate < currentEndExclusive)
                    ))
                .Select(o => new
                {
                    Period = o.DeliveredDate >= s && o.DeliveredDate < currentEndExclusive ? 0 : 1
                })
                .GroupBy(x => x.Period)
                .Select(g => new
                {
                    Period = g.Key,
                    Fulfilled = g.Count()
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var deliveredGrouped = await db.OrderItems
                .AsNoTracking()
                .Where(oi =>
                    oi.Order != null &&
                    oi.Order.Status == OrderStatus.DELIVERED &&
                    (
                        (oi.Order.DeliveredDate >= previousStart && oi.Order.DeliveredDate < previousEndExclusive) ||
                        (oi.Order.DeliveredDate >= s && oi.Order.DeliveredDate < currentEndExclusive)
                    ))
                .Select(oi => new
                {
                    Period = oi.Order.DeliveredDate >= s && oi.Order.DeliveredDate < currentEndExclusive ? 0 : 1,
                    Sales = oi.Quantity * oi.UnitPrice,
                    Units = oi.Quantity
                })
                .GroupBy(x => x.Period)
                .Select(g => new
                {
                    Period = g.Key,
                    Sales = g.Sum(x => x.Sales),
                    Units = g.Sum(x => x.Units)
                })
                .ToListAsync()
                .ConfigureAwait(false);

            int curCount = 0, prevCount = 0;
            int curFulfilled = 0, prevFulfilled = 0;
            int curPending = 0, prevPending = 0;
            int curReturned = 0, prevReturned = 0;
            decimal curRevenue = 0m, prevRevenue = 0m;
            decimal curSales = 0m, prevSales = 0m;
            int curUnits = 0, prevUnits = 0;

            foreach (var g in ordersGrouped)
            {
                if (g.Period == 0)
                {
                    curRevenue = g.Revenue;
                    curCount = g.Count;
                    curPending = g.Pending;
                    curReturned = g.Returned;
                }
                else
                {
                    prevRevenue = g.Revenue;
                    prevCount = g.Count;
                    prevPending = g.Pending;
                    prevReturned = g.Returned;
                }
            }

            foreach (var g in fulfilledGrouped)
            {
                if (g.Period == 0) curFulfilled = g.Fulfilled;
                else prevFulfilled = g.Fulfilled;
            }

            foreach (var g in deliveredGrouped)
            {
                if (g.Period == 0)
                {
                    curSales = g.Sales;
                    curUnits = g.Units;
                }
                else
                {
                    prevSales = g.Sales;
                    prevUnits = g.Units;
                }
            }

            var ts = await BuildRevenueAndOrdersTimeSeriesAsync(startDate, endDate, db);

            return new OrdersAnalytics
            {
                TotalOrders = curCount,
                TotalRevenue = curRevenue,
                TotalSales = curSales,
                FulfilledOrders = curFulfilled,
                PendingOrders = curPending,
                ReturnedOrders = curReturned,
                UnitsSold = curUnits,

                TotalOrdersTrend = CalculateTrend(curCount, prevCount),
                RevenueTrend = CalculateTrend(curRevenue, prevRevenue, isCurrency: true),
                SalesTrend = CalculateTrend(curSales, prevSales, isCurrency: true),
                FulfilledTrend = CalculateTrend(curFulfilled, prevFulfilled),
                PendingTrend = CalculateTrend(curPending, prevPending),

                RevenueChangePercent = CalculatePercent(curRevenue, prevRevenue),
                OrdersChangePercent = CalculatePercent(curCount, prevCount),

                RevenueAndOrdersSeries = ts
            };
        }

        public async Task<IEnumerable<TopCustomer>> GetTopCustomersAsync(int topCount = 5)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Where(o => o.Status == OrderStatus.DELIVERED)
                .Include(o => o.User)
                .GroupBy(o => o.UserId)
                .Select(g => new TopCustomer
                {
                    UserId = g.Key!,
                    Name = g.First().User.FirstName + " " + g.First().User.LastName ?? "N/A",
                    Email = g.First().User.Email!,
                    TotalSpent = g.SelectMany(o => o.OrderItems)
                          .Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(tc => tc.TotalSpent)
                .Take(topCount)
                .ToListAsync();
        }

        public async Task<FulfillmentMetricsDto> GetFulfillmentMetricsAsync(DateTime startDate, DateTime endDate, double targetHours = 24)
        {
            await using var _db = _dbContextFactory.CreateDbContext();

            var items = await _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == OrderStatus.DELIVERED)
                .Select(o => new
                {
                    Start = o.OrderDate,
                    End = /*o.DeliveredDate ??*/ (DateTime?)o.OrderDate.AddDays(3) 
                })
                .ToListAsync();

            foreach (var item in items)
            {
                Console.WriteLine($"{item.Start}; {item.End}");
            }

            if (items.Count == 0)
                return new FulfillmentMetricsDto { AvgProcessingHours = 0, AvgProcessingTimePercent = 0 };

            var hoursList = items
                .Where(i => i.End.HasValue)
                .Select(i => (i.End!.Value - i.Start).TotalHours)
                .ToList();

            var avg = hoursList.Count != 0 ? hoursList.Average() : 0;
            var percent = targetHours > 0 ? avg / targetHours * 100.0 : 0;

            return new FulfillmentMetricsDto
            {
                AvgProcessingHours = Math.Round(avg, 2),
                AvgProcessingTimePercent = Math.Round(percent, 2)
            };
        }

        public async Task<OrderStatusCountsDto> GetOrderStatusCountsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            await using var _db = _dbContextFactory.CreateDbContext();

            var q = _db.Orders.AsQueryable();

            if (startDate.HasValue)
                q = q.Where(o => o.OrderDate >= startDate.Value.Date);
            if (endDate.HasValue)
                q = q.Where(o => o.OrderDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var counts = await q
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var dto = new OrderStatusCountsDto
            {
                Pending = counts.FirstOrDefault(x=> x.Status == OrderStatus.PENDING)?.Count ?? 0,
                Processing = counts.FirstOrDefault(x => x.Status == OrderStatus.PROCESSING)?.Count ?? 0,
                Shipped = counts.FirstOrDefault(x => x.Status == OrderStatus.SHIPPED)?.Count ?? 0,
                Delivered = counts.FirstOrDefault(x => x.Status == OrderStatus.DELIVERED)?.Count ?? 0,
                Cancelled = counts.FirstOrDefault(x => x.Status == OrderStatus.CANCELLED)?.Count ?? 0,
                Returned = counts.FirstOrDefault(x => x.Status == OrderStatus.RETURNED)?.Count ?? 0
            };

            return dto;
        }

        public async Task<TimeSeriesDto> GetRevenueTimeSeriesAsync(DateTime startDate, DateTime endDate, string groupBy = "day")
        {
            using var _db = _dbContextFactory.CreateDbContext();

            var fullSeries = await BuildRevenueAndOrdersTimeSeriesAsync(startDate, endDate, _db);

            var revenueSeries = fullSeries.Series.FirstOrDefault(s => s.Name == "Revenue");

            return new TimeSeriesDto
            {
                Labels = fullSeries.Labels,
                Series = revenueSeries is null
                    ? []
                    : [revenueSeries]
            };
        }

        public async Task<IEnumerable<TopProduct>> GetTopProductsAsync(DateTime startDate, DateTime endDate, int topCount = 5)
        {
            await using var db = _dbContextFactory.CreateDbContext();

            if (endDate < startDate) (startDate, endDate) = (endDate, startDate);

            var aggregated = await db.OrderItems
                .Where(oi =>
                    oi.Order != null &&
                    oi.Order.Status == OrderStatus.DELIVERED &&
                    oi.Order.OrderDate >= startDate &&
                    oi.Order.OrderDate <= endDate)
                .GroupBy(oi => new { oi.ProductId, ProductName = oi.Product.Name, oi.Product.ImagePath, oi.Product.ImageUrl,oi.Product.Price })
                .Select(g => new 
                {
                    Id = g.Key.ProductId,
                    Name = g.Key.ProductName,
                    ImagePath = g.Key.ImagePath,
                    ImageUrl = g.Key.ImageUrl,
                    Price = g.Key.Price,
                    UnitsSold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topCount)
                .ToListAsync();

            if (aggregated.Count == 0)
                return new List<TopProduct>();

            var productIds = aggregated.Select(x => x.Id).Distinct().ToList();

            // 2) Fetch category names for those product ids (pick first category if multiple)
            // Join ProductCategories -> Categories to get category name (assumes you have a Categories DbSet with Name)
            var categoryMap = await db.ProductCategories
                .Where(pc => productIds.Contains(pc.ProductId))
                .Join(db.Set<Category>(), pc => pc.CategoryId, c => c.Id, (pc, c) => new { pc.ProductId, CategoryName = c.Name })
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, CategoryName = g.Select(x => x.CategoryName).FirstOrDefault() })
                .ToDictionaryAsync(x => x.ProductId, x => x.CategoryName);

            var result = aggregated.Select(x => new TopProduct
            {
                Id = x.Id,
                Name = x.Name ?? string.Empty,
                Category = categoryMap.TryGetValue(x.Id, out var cat) ? (cat ?? string.Empty) : string.Empty,
                ImageUrl = x.ImageUrl ?? x.ImagePath ?? string.Empty,
                Price = x.Price,
                UnitsSold = x.UnitsSold,
                Revenue = x.Revenue
            })
            .ToList();

            return result;
        }


        //Helpers
        private static string CalculateTrend(decimal current, decimal previous, bool isCurrency = false)
        {
            if (previous == 0)
            {
                if (current == 0) return "No change vs previous period";
                return isCurrency ? $"+{current:N2} Ksh vs previous period (no prior data)" : $"+{Math.Round(current, 1)} vs previous period (no prior data)";
            }

            var changePercent = (current - previous) / previous * 100m;
            var sign = changePercent >= 0 ? "+" : "-";
            var pct = Math.Round(Math.Abs(changePercent), 1).ToString() + "%";

            if (isCurrency)
            {
                var diff = current - previous;
                var diffFormatted = diff >= 0 ? $"+{diff:N2} ksh" : $"{diff:N2} Ksh";
                return $"{sign}{pct} ({diffFormatted}) vs previous period";
            }
            else
            {
                return $"{sign}{pct} vs previous period";
            }
        }

        private static async Task<TimeSeriesDto> BuildRevenueAndOrdersTimeSeriesAsync(DateTime startDate, DateTime endDate, ApplicationDbContext _db)
        {
            startDate = startDate.Date;
            endDate = endDate.Date;

            var totalDays = (endDate - startDate).TotalDays + 1;

            string groupBy;
            string labelFormat;
            if (totalDays == 1)
            {
                groupBy = "hour";
                labelFormat = "HH:mm";   
            }
            else if (totalDays <= 31)
            {
                groupBy = "day";
                labelFormat = "MMM dd";
            }
            else if (totalDays <= 180)
            {
                groupBy = "week";
                labelFormat = "'W'w yyyy";
            }
            else
            {
                groupBy = "month";
                labelFormat = "MMM yyyy";
            }

            var endExclusive = endDate.AddDays(1);

            var ordersQuery = _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate < endExclusive);

            var revenueQuery = _db.OrderItems
            .Where(oi => oi.Order.DeliveredDate != null
                         && oi.Order.DeliveredDate >= startDate
                         && oi.Order.DeliveredDate < endExclusive
                         && oi.Order.Status == OrderStatus.DELIVERED);

            List<(DateTime Period, int Orders)> ordersGrouped;
            if (groupBy == "hour")
            {
                var q = await ordersQuery
                    .GroupBy(o => new DateTime(o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day, o.OrderDate.Hour, 0, 0))
                    .Select(g => new { Period = g.Key, Orders = g.Count() })
                    .ToListAsync();

                ordersGrouped = [.. q.Select(x => (x.Period, x.Orders))];
            }
            else if (groupBy == "week")
            {
                var q = await ordersQuery
                    .GroupBy(o =>
                        EF.Functions.DateFromParts(o.OrderDate.Year, 1, 1)
                        .AddDays((EF.Functions.DateDiffDay(EF.Functions.DateFromParts(o.OrderDate.Year, 1, 1), o.OrderDate) / 7) * 7))
                    .Select(g => new { Period = g.Key, Orders = g.Count() })
                    .ToListAsync();

                ordersGrouped = [.. q.Select(x => (x.Period, x.Orders))];
            }
            else if (groupBy == "month")
            {
                var q = await ordersQuery
                    .GroupBy(o => new DateTime(o.OrderDate.Year, o.OrderDate.Month, 1))
                    .Select(g => new { Period = g.Key, Orders = g.Count() })
                    .ToListAsync();

                ordersGrouped = [.. q.Select(x => (x.Period, x.Orders))];
            }
            else 
            {
                var q = await ordersQuery
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new { Period = g.Key, Orders = g.Count() })
                    .ToListAsync();

                ordersGrouped = [.. q.Select(x => (x.Period, x.Orders))];
            }

            List<(DateTime Period, decimal Revenue)> revenueGrouped;
            if (groupBy == "hour")
            {
                var q = await revenueQuery
                    .GroupBy(oi => new DateTime(oi.Order.DeliveredDate.Year, oi.Order.DeliveredDate.Month, oi.Order.DeliveredDate.Day, oi.Order.DeliveredDate.Hour, 0, 0))
                    .Select(g => new { Period = g.Key, Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                    .ToListAsync();

                revenueGrouped = [.. q.Select(x => (x.Period, x.Revenue))];
            }
            else if (groupBy == "week")
            {
                var q = await revenueQuery
                    .GroupBy(oi => EF.Functions.DateFromParts(oi.Order.DeliveredDate.Year, 1, 1)
                        .AddDays(EF.Functions.DateDiffDay(EF.Functions.DateFromParts(oi.Order.DeliveredDate.Year, 1, 1), oi.Order.DeliveredDate) / 7 * 7))
                    .Select(g => new { Period = g.Key, Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                    .ToListAsync();

                revenueGrouped = [.. q.Select(x => (x.Period, x.Revenue))];
            }
            else if (groupBy == "month")
            {
                var q = await revenueQuery
                    .GroupBy(oi => new DateTime(oi.Order.DeliveredDate.Year, oi.Order.DeliveredDate.Month, 1))
                    .Select(g => new { Period = g.Key, Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                    .ToListAsync();

                revenueGrouped = [.. q.Select(x => (x.Period, x.Revenue))];
            }
            else
            {
                var q = await revenueQuery
                    .GroupBy(oi => oi.Order.DeliveredDate.Date)
                    .Select(g => new { Period = g.Key, Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                    .ToListAsync();

                revenueGrouped = [.. q.Select(x => (x.Period, x.Revenue))];
            }

            List<DateTime> periods;
            if (groupBy == "hour")
            {
                periods = [.. Enumerable.Range(0, 24).Select(i => startDate.AddHours(i))];
            }
            else if (groupBy == "day")
            {
                periods = [.. Enumerable.Range(0, (int)totalDays).Select(i => startDate.AddDays(i))];
            }
            else if (groupBy == "week")
            {
                var numWeeks = (int)Math.Ceiling(totalDays / 7.0);
                periods = [.. Enumerable.Range(0, numWeeks).Select(i => startDate.AddDays(i * 7))];
            }
            else
            {
                var monthsCount = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month + 1;
                periods = [.. Enumerable.Range(0, monthsCount).Select(i => new DateTime(startDate.Year, startDate.Month, 1).AddMonths(i))];
            }

            var labels = periods.Select(p => p.ToString(labelFormat)).ToArray();

            var ordersDict = ordersGrouped.ToDictionary(x => x.Period, x => x.Orders);
            var revenueDict = revenueGrouped.ToDictionary(x => x.Period, x => x.Revenue);

            var ordersData = periods
                .Select(p => ordersDict.TryGetValue(p, out var v) ? v : 0.0)
                .ToArray();

            var revenueData = periods
                .Select(p => revenueDict.TryGetValue(p, out var r) ? (double)r : 0.0)
                .ToArray();


            return new TimeSeriesDto
            {
                Labels = labels,
                Series =
                [
                    new SeriesDto { Name = "Revenue(Delivered Orders)", Data = revenueData },
                    new SeriesDto { Name = "Orders", Data = ordersData }
                ]
            };
        }


        // Helper to compute percent change as double (rounded to 2 decimals)
        private static double CalculatePercent(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current == 0m ? 0.0 : 100.0;
            }
            var change = (current - previous) / previous * 100m;
            return Math.Round((double)change, 2);
        }

        private static double CalculatePercent(int current, int previous)
        {
            if (previous == 0)
            {
                return current == 0 ? 0.0 : 100.0;
            }
            var change = (double)(current - previous) / previous * 100.0;
            return Math.Round(change, 2);
        }

        
    }

}
