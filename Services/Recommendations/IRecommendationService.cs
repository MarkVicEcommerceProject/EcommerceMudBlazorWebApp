namespace ECommerceMudblazorWebApp.Services.Recommendations
{
    using ECommerceMudblazorWebApp.Data;
    using ECommerceMudblazorWebApp.Models;
    using Microsoft.EntityFrameworkCore;

    public interface IRecommendationService
    {
        Task<IEnumerable<Product>> GetFlashSalesAsync(DateTime? referenceDate = null, int limit = 20);
        Task<IEnumerable<DailyDealDto>> GetDailyDealsAsync(DateTime date, int limit = 5);
        Task<IEnumerable<Product>> GetTrendingNowAsync(DateTime from, DateTime to, int limit = 20);
        Task<IEnumerable<Product>> GetNewArrivalsAsync(TimeSpan window, int limit = 20);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int limit = 10);
        Task<IEnumerable<Product>> GetYouMayAlsoLikeAsync(string? userId, List<int> cartProductIds, int limit = 12, ApplicationDbContext? externalDb = null);
        Task<IEnumerable<Product>> GetSuggestionsAfterPurchaseAsync(int orderId, int limit = 12);
        Task<IEnumerable<Product>> GetRecentlyBoughtByUserAsync(string? userId, int limit = 20);
        Task<IEnumerable<Product>> GetFeaturedProductsAsync(int limit = 20);

        //Admin Actions
        Task SetProductFeaturedAsync(int productId, bool isFeatured, DateTime? startDate = null, DateTime? endDate = null, int position = 0, ApplicationDbContext? externalDb = null);
        Task RemoveFeaturedProductAsync(int productId, ApplicationDbContext? externalDb = null);
        Task<DailyDeal> SetProductAsDailyDealAsync(int productId, decimal? dealPrice, DateTime? date = null, int priority = 0, DateTime? startAt = null, DateTime? endAt = null, ApplicationDbContext? externalDb = null);
        Task RemoveDailyDealAsync(int productId, DateTime? date = null, ApplicationDbContext? externalDb = null);


        Task AddOrUpdateFlashSaleItemAsync(int? flashSaleId, int productId, decimal salePrice, int priority = 0, DateTime? saleStart = null, DateTime? saleEnd = null, string? saleName = null, ApplicationDbContext? externalDb = null);

        Task RemoveProductFromAnyFlashSaleAsync(int productId, ApplicationDbContext? externalDb = null);
        Task SyncProductFlashFieldsFromFlashSaleAsync(int productId);

    }
}
