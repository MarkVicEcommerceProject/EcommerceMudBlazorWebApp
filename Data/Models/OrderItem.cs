﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Data.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName ="decimal(18,2)")]
        public int UnitPrice { get; set; }

        [Required]
        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        [Required]
        [ForeignKey("OrderId")]
        public Order Order { get; set; }     
    }
}
