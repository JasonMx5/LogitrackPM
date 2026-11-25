using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Security
{
    public class AuthRateLimitFilter : IAsyncActionFilter
    {
        private const int RequestLimit = 10;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _cache;

        public AuthRateLimitFilter(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
            var key = "auth-rate-" + (remoteIp == null ? "unknown" : remoteIp.ToString());
            var counter = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Window;
                return new RequestCounter();
            });

            lock (counter)
            {
                if (counter.Count >= RequestLimit)
                {
                    context.HttpContext.Response.Headers["Retry-After"] = "60";
                    context.Result = new ObjectResult(new
                    {
                        message = "Too many authentication requests. Try again later."
                    })
                    {
                        StatusCode = 429
                    };
                    return;
                }

                counter.Count++;
            }

            await next();
        }

        private sealed class RequestCounter
        {
            public int Count { get; set; }
        }
    }
}
