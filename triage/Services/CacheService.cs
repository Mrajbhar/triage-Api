using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace triage.Services
{
   
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan ttl);
        Task RemoveAsync(string key);
    }

    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<CacheService> _log;

        public CacheService(IDistributedCache cache, ILogger<CacheService> log)
        {
            _cache = cache;
            _log = log;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var raw = await _cache.GetStringAsync(key);
                return raw is null ? default : JsonSerializer.Deserialize<T>(raw);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cache GET failed for {Key} — falling back to source.", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                await _cache.SetStringAsync(key, json,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cache SET failed for {Key} — proceeding without cache.", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try { await _cache.RemoveAsync(key); }
            catch (Exception ex) { _log.LogWarning(ex, "Cache REMOVE failed for {Key}.", key); }
        }
    }
}