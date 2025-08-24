namespace ECommerceMudblazorWebApp.Models
{
    public class CustomerAnalytics
    {
        public int TotalCustomers { get; set; }
        public string CustomerChange { get; set; } = string.Empty;
    }

    public class OrdersAnalytics
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalSales { get; set; }
        public int FulfilledOrders { get; set; }
        public int PendingOrders { get; set; }

        //Trends
        public string TotalOrdersTrend { get; set; } = "+0%";
        public string RevenueTrend { get; set; } = "+0%";

        public string SalesTrend { get; set; } = "+0%";
        public string FulfilledTrend { get; set; } = "+0%";
        public string PendingTrend { get; set; } = "+0%";

        public int ReturnedOrders { get; set; }
        public int UnitsSold { get; set; }
        public double RevenueChangePercent { get; set; } 
        public double OrdersChangePercent { get; set; }
        public TimeSeriesDto RevenueAndOrdersSeries { get; set; } = new();


    }

    public class TopCustomer
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(n => n[0]))
                                        .ToUpper();
    }

    public class SeriesDto
    {
        public string Name { get; set; } = string.Empty;
        public double[] Data { get; set; } = [];
    }

    public class TimeSeriesDto
    {
        public string[] Labels { get; set; } = [];
        public List<SeriesDto> Series { get; set; } = new();
    }

    public class FulfillmentMetricsDto
    {
        public double AvgProcessingHours { get; set; }
        public double AvgProcessingTimePercent { get; set; }
    }

    public class OrderStatusCountsDto
    {
        public int Pending { get; set; }
        public int Processing { get; set; }
        public int Shipped { get; set; }
        public int Delivered { get; set; }
        public int Cancelled { get; set; }
        public int Returned { get; set; }
    }

    public class MetricsData
    {
        public double ConversionRate { get; set; }
        public double ConversionChange { get; set; }
        public decimal AverageOrderValue { get; set; }
        public double AovChange { get; set; }
        public double ReturnRate { get; set; }
        public double ReturnChange { get; set; }
    }

    public class SecurityStatus
    {
        public bool IsSecure { get; set; }
        public bool PciCompliant { get; set; }
        public bool Encrypted { get; set; }
    }

    public class AiInsightsData
    {
        public decimal PredictedRevenue { get; set; }
    }

    public class TopProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int UnitsSold { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class InventoryAlert
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int StockLeft { get; set; }
    }

}
