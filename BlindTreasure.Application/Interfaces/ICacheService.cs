using System;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task<bool> SetAsync<T>(string key, T value, TimeSpan expiration);
        Task<bool> RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
        
        // For atomic operations
        Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiration);
        Task<bool> UpdateIfExistsAsync<T>(string key, T value, TimeSpan? expiration = null);
        
        // For collection management
        Task<long> ListAddAsync<T>(string key, T value, bool createIfNotExists = true);
        Task<IEnumerable<T>> ListRangeAsync<T>(string key, int start = 0, int stop = -1);
        
        // Pattern-based operations
        Task<IEnumerable<string>> FindKeysByPatternAsync(string pattern);
        Task<long> RemoveByPatternAsync(string pattern);
    }
    
    // Extension methods for common caching patterns
    public static class CacheServiceExtensions
    {
        // Optimistic read with refresh pattern
        public static async Task<T?> GetOrRefreshAsync<T>(
            this ICacheService cache,
            string cacheKey, 
            Func<Task<T>> dataFactory, 
            TimeSpan expiration) where T : class
        {
            var cachedData = await cache.GetAsync<T>(cacheKey);
            if (cachedData != null)
                return cachedData;
                
            var data = await dataFactory();
            if (data != null)
                await cache.SetAsync(cacheKey, data, expiration);
                
            return data;
        }
        
        // Cache invalidation helper for entity changes
        public static async Task InvalidateEntityCacheAsync<T>(
            this ICacheService cache,
            string entityType,
            string identifier)
        {
            var pattern = $"{entityType}:{identifier}*";
            await cache.RemoveByPatternAsync(pattern);
        }
        
        // Atomic update with validation
        public static async Task<bool> TryUpdateAsync<T>(
            this ICacheService cache,
            string key,
            T value,
            TimeSpan expiration,
            Func<T, bool> validator) where T : class
        {
            var current = await cache.GetAsync<T>(key);
            if (current == null || !validator(current))
                return false;
                
            return await cache.SetAsync(key, value, expiration);
        }
    }
}