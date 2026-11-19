using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LogiTrack.Models;

namespace LogiTrack
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var sampleItem = new InventoryItem
            {
                ItemId = 1,
                Name = "Pallet Jack",
                Quantity = 12,
                Location = "Warehouse A"
            };
            sampleItem.DisplayInfo();

            var order = new Order
            {
                OrderId = 1001,
                CustomerName = "Samir",
                DatePlaced = new DateTime(2025, 4, 5)
            };

            order.AddItem(new InventoryItem
            {
                ItemId = 1,
                Name = "Pallet Jack",
                Quantity = 2,
                Location = "Warehouse A"
            });
            order.AddItem(new InventoryItem
            {
                ItemId = 2,
                Name = "Forklift",
                Quantity = 1,
                Location = "Warehouse B"
            });
            order.AddItem(new InventoryItem
            {
                ItemId = 3,
                Name = "Shipping Tape",
                Quantity = 20,
                Location = "Warehouse A"
            });

            order.RemoveItem(3);

            Console.WriteLine(order.GetOrderSummary());

            using (var context = new LogiTrackContext())
            {
                // Add test inventory item if none exist
                if (!context.InventoryItems.Any())
                {
                    context.InventoryItems.Add(new InventoryItem
                    {
                        Name = "Pallet Jack",
                        Quantity = 12,
                        Location = "Warehouse A"
                    });

                    context.SaveChanges();
                }

                // Retrieve and print inventory to confirm
                var items = context.InventoryItems.ToList();
                foreach (var item in items)
                {
                    item.DisplayInfo();
                }
            }

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
