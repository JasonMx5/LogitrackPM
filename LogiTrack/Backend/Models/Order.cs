using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        public string CustomerName { get; set; }

        public DateTime DatePlaced { get; set; }

        public string UserId { get; set; }

        public List<OrderItem> Items { get; set; }

        public Order()
        {
            Items = new List<OrderItem>();
        }
    }
}
