using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogiTrack.Caching;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers
{
    [Route("api/inventory")]
    [ApiController]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;

        public InventoryController(LogiTrackContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> Get()
        {
            if (!_cache.TryGetValue(CacheKeys.InventoryItems, out List<InventoryItem> items))
            {
                items = await _context.InventoryItems
                    .AsNoTracking()
                    .ToListAsync();
                _cache.Set(
                    CacheKeys.InventoryItems,
                    items,
                    TimeSpan.FromSeconds(30));
            }

            return items;
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<InventoryItem>> Post([FromBody] InventoryItem item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.InventoryItems);

            return Ok(item);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.InventoryItems);

            return NoContent();
        }
    }
}