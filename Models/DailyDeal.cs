using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class DailyDeal
    {
        [Key] public int Id { get; set; }
        public DateTime Date { get; set; } 
        public int ProductId { get; set; }
        public int Priority { get; set; } = 0;
        [Column(TypeName = "decimal(18,2)")] public decimal? DealPrice { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
    }

}
