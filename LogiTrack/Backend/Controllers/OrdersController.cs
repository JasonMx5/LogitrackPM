using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LogiTrack.Caching;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers
{
    [Route("api/orders")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private const string OrdersListCacheVersionKey = "orders-list-version";
        private const int DefaultPageSize = 20;
        private const int MaximumPageSize = 100;

        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;

        public OrdersController(LogiTrackContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize)
        {
            if (page < 1 || pageSize < 1 || pageSize > MaximumPageSize)
            {
                return BadRequest("Page must be positive and pageSize must be between 1 and 100.");
            }

            var skip = (long)(page - 1) * pageSize;
            if (skip > int.MaxValue)
            {
                return BadRequest("Page is too large.");
            }

            var isManager = User.IsInRole("Manager");
            var userId = GetCurrentUserId();
            if (!isManager && userId == null)
            {
                return Unauthorized();
            }

            var cacheScope = isManager ? "manager" : userId;
            var cacheKey = GetOrdersPageCacheKey(page, pageSize, cacheScope);
            if (!_cache.TryGetValue(cacheKey, out List<Order> orders))
            {
                var query = _context.Orders.AsQueryable();
                if (!isManager)
                {
                    query = query.Where(order => order.UserId == userId);
                }

                orders = await query
                    .OrderBy(order => order.OrderId)
                    .Include(order => order.Items)
                    .AsNoTracking()
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToListAsync();
                ClearOrderReferences(orders.SelectMany(order => order.Items));

                _cache.Set(cacheKey, orders, TimeSpan.FromSeconds(30));
            }

            return orders;
        }

        [HttpGet("{id}", Name = "GetOrderById")]
        public async Task<ActionResult<Order>> GetById(int id)
        {
            var isManager = User.IsInRole("Manager");
            var userId = GetCurrentUserId();
            if (!isManager && userId == null)
            {
                return Unauthorized();
            }

            var cacheScope = isManager ? "manager" : userId;
            var cacheKey = GetOrderCacheKey(id, cacheScope);
            if (!_cache.TryGetValue(cacheKey, out Order order))
            {
                var query = _context.Orders.AsQueryable();
                if (!isManager)
                {
                    query = query.Where(candidate => candidate.UserId == userId);
                }

                order = await query
                    .Include(candidate => candidate.Items)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.OrderId == id);

                if (order == null)
                {
                    return NotFound();
                }

                ClearOrderReferences(order.Items);

                _cache.Set(cacheKey, order, TimeSpan.FromSeconds(30));
            }

            return order;
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                var cart = await _context.ShoppingCarts
                    .Include(candidate => candidate.Items)
                    .ThenInclude(item => item.InventoryItem)
                    .FirstOrDefaultAsync(candidate => candidate.UserId == userId);

                if (cart == null)
                {
                    return BadRequest(new { message = "The cart is empty." });
                }

                if (cart.ExpiresAt <= DateTime.UtcNow)
                {
                    _context.ShoppingCarts.Remove(cart);
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    return Conflict(new { message = "The cart has expired." });
                }

                if (cart.Items.Count == 0)
                {
                    return BadRequest(new { message = "The cart is empty." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Unauthorized();
                }

                var order = new Order
                {
                    UserId = userId,
                    CustomerName = user.Email ?? user.UserName,
                    DatePlaced = DateTime.UtcNow
                };

                foreach (var cartItem in cart.Items)
                {
                    var updatedRows = await _context.Database.ExecuteSqlCommandAsync(
                        "UPDATE \"InventoryItems\" " +
                        "SET \"Quantity\" = \"Quantity\" - {0} " +
                        "WHERE \"ItemId\" = {1} AND \"Quantity\" >= {0}",
                        cartItem.Quantity,
                        cartItem.InventoryItemId);

                    if (updatedRows != 1)
                    {
                        transaction.Rollback();
                        return Conflict(new
                        {
                            message = "Requested quantity is no longer available.",
                            inventoryItemId = cartItem.InventoryItemId
                        });
                    }

                    order.Items.Add(new OrderItem
                    {
                        InventoryItemId = cartItem.InventoryItemId,
                        Name = cartItem.InventoryItem.Name,
                        Quantity = cartItem.Quantity,
                        Location = cartItem.InventoryItem.Location
                    });
                }

                _context.Orders.Add(order);
                _context.ShoppingCarts.Remove(cart);
                await _context.SaveChangesAsync();
                transaction.Commit();

                InvalidateOrdersCache();
                _cache.Remove(CacheKeys.InventoryItems);

                return CreatedAtRoute("GetOrderById", new { id = order.OrderId }, null);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(candidate => candidate.OrderId == id);
            if (order == null)
            {
                return NotFound();
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            InvalidateOrdersCache();

            return NoContent();
        }

        private string GetOrderCacheKey(int id, string scope)
        {
            return "orders-" + GetOrdersCacheVersion() + "-" + scope + "-" + id;
        }

        private string GetOrdersPageCacheKey(int page, int pageSize, string scope)
        {
            return "orders-list-" + GetOrdersCacheVersion() + "-" + scope + "-" + page + "-" + pageSize;
        }

        private string GetOrdersCacheVersion()
        {
            if (!_cache.TryGetValue(OrdersListCacheVersionKey, out string version))
            {
                version = Guid.NewGuid().ToString("N");
                _cache.Set(OrdersListCacheVersionKey, version);
            }

            return version;
        }

        private void InvalidateOrdersCache()
        {
            _cache.Set(OrdersListCacheVersionKey, Guid.NewGuid().ToString("N"));
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private static void ClearOrderReferences(IEnumerable<OrderItem> items)
        {
            foreach (var item in items)
            {
                item.Order = null;
            }
        }
    }
}