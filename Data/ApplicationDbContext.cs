using ECommerceMudblazorWebApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace ECommerceMudblazorWebApp.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ProductTag> ProductTags { get; set; }
        public DbSet<ProductDailyStat> ProductDailyStat { get; set; }
        public DbSet<ProductView> ProductViews { get; set; }
        public DbSet<FeaturedProduct> FeaturedProducts { get; set; }
        public DbSet<DailyDeal> DailyDeals { get; set; }
        public DbSet<FlashSale> FlashSales { get; set; }
        public DbSet<FlashSaleItem> FlashSaleItems { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.CreatedAt);
                entity.HasIndex(p => p.SKU).IsUnique();
                entity.HasIndex(p => p.IsFeatured);
                entity.HasIndex(p => p.IsFlashSale);
                entity.HasIndex(p => p.IsActive);
                entity.HasIndex(p => p.Price);
                entity.HasIndex(p => new { p.IsActive, p.IsFeatured });
                entity.HasIndex(p => new { p.IsActive, p.IsFlashSale });
            });
            // ProductCategory indices
            builder.Entity<ProductCategory>(entity =>
            {
                entity.HasIndex(pc => pc.CategoryId);
                entity.HasIndex(pc => new { pc.ProductId, pc.CategoryId }).IsUnique();
            });

            builder.Entity<ProductTag>(entity =>
            {
                entity.HasIndex(pt => pt.TagId);
                entity.HasIndex(pt => pt.ProductId);
                entity.HasIndex(pt => new { pt.ProductId, pt.TagId }).IsUnique(); // prevent duplicate tag links
            });

            builder.Entity<Tag>(entity =>
            {
                entity.HasIndex(t => t.Name).IsUnique(); // fast lookup + uniqueness of tag names
            });

            // OrderItem indices
            builder.Entity<OrderItem>(entity =>
            {
                entity.HasIndex(oi => oi.ProductId);
                entity.HasIndex(oi => oi.OrderId);
                entity.HasIndex(oi => new { oi.ProductId, oi.OrderId });
                entity.HasIndex(oi => new { oi.OrderId, oi.ProductId });

            });

            // ProductView indices
            builder.Entity<ProductView>(entity =>
            {
                entity.HasIndex(pv => pv.ProductId);
                entity.HasIndex(pv => pv.ViewedAt);
                entity.HasIndex(pv => new { pv.ProductId, pv.ViewedAt });
            });

            // ProductDailyStat indices
            builder.Entity<ProductDailyStat>(entity =>
            {
                entity.HasIndex(pds => pds.ProductId);
                entity.HasIndex(pds => pds.Date);
                entity.HasIndex(pds => new { pds.ProductId, pds.Date }).IsUnique();
            });

            
            builder.Entity<DailyDeal>(entity =>
            {
                entity.HasIndex(dd => dd.Date);
                entity.HasIndex(dd => dd.ProductId);
                entity.HasIndex(dd => dd.Priority);
                entity.HasIndex(dd => new { dd.StartAt, dd.EndAt });
                entity.HasIndex(dd => new { dd.Date, dd.Priority });
                entity.HasIndex(dd => new { dd.Date, dd.ProductId }).IsUnique(); // One deal per product per day
            });

            // FeaturedProduct indices - CORRECTED
            builder.Entity<FeaturedProduct>(entity =>
            {
                entity.HasIndex(fp => fp.ProductId);
                entity.HasIndex(fp => fp.Position);
                entity.HasIndex(fp => new { fp.StartDate, fp.EndDate });
                entity.HasIndex(fp => new { fp.Position, fp.ProductId });
                entity.HasIndex(fp => new { fp.StartDate, fp.EndDate, fp.Position }); // For active featured products query
            });

            builder.Entity<FlashSale>(entity =>
            {
                entity.HasIndex(fs => new { fs.StartAt, fs.EndAt });
                // Optionally create computed StartDate and unique index if you want "one flash sale per day"
                // e.g., entity.HasIndex(fs => fs.StartDate).IsUnique();
            });

            builder.Entity<FlashSaleItem>(entity =>
            {
                entity.HasIndex(fsi => fsi.FlashSaleId);
                entity.HasIndex(fsi => fsi.ProductId);
                entity.HasIndex(fsi => new { fsi.FlashSaleId, fsi.ProductId }).IsUnique(); // prevent duplicate item entries
            });
        }
        
       
    }
}
