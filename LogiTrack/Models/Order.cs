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

        public List<InventoryItem> Items { get; set; }

        public Order()
        {
            Items = new List<InventoryItem>();
        }

        public void AddItem(InventoryItem item)
        {
            if (item == null)
            {
                return;
            }

            Items.Add(item);
        }

        public void RemoveItem(int itemId)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].ItemId == itemId)
                {
                    Items.RemoveAt(i);
                    return;
                }
            }
        }

        public string GetOrderSummary()
        {
            return $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced.ToString("M/d/yyyy")}";
        }
    }
}
