using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class FlashSale
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public ICollection<FlashSaleItem> Items { get; set; } = new List<FlashSaleItem>();
    }
    public class FlashSaleItem
    {
        [Key] public int Id { get; set; }
        public int FlashSaleId { get; set; }
        public int ProductId { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal SalePrice { get; set; }
        public int Priority { get; set; } = 0;
        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
        [ForeignKey(nameof(FlashSaleId))] public FlashSale FlashSale { get; set; }
    }

}
