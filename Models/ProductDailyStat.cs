using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class ProductDailyStat
    {
        [Key] public int Id { get; set; }
        public int ProductId { get; set; }
        public DateTime Date { get; set; }
        public int Views { get; set; }
        public int Sales { get; set; }

        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
    }

}
