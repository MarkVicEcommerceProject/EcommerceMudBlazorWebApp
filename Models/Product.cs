namespace ECommerceMudblazorWebApp.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(50)]
        public string SKU { get; set; }

        [Required]
        public int StockQuantity { get; set; }

        [StringLength(255)]
        public string ImagePath { get; set; }

        [StringLength(255)]
        public string? ImageUrl { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        public bool IsFeatured { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsDailyDeal { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DailyDealPrice { get; set; }
        public bool IsFlashSale { get; set; } = false;  

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FlashSalePrice { get; set; }
        public DateTime? FlashSaleStart { get; set; }
        public DateTime? FlashSaleEnd { get; set; }
        public int ViewsCount { get; set; } = 0;             
        public int TotalSalesCount { get; set; } = 0;


        public List<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
        public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();


    }
}
