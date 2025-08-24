namespace ECommerceMudblazorWebApp.Models
{
    public class DailyDealDto
    {
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public decimal DealPrice { get; set; }         
        public DateTime Date { get; set; }              
        public DateTime StartAt { get; set; }           
        public DateTime EndAt { get; set; }             
        public int Priority { get; set; }
    }

}
