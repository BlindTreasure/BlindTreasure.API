using BlindTreasure.Application.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonException = System.Text.Json.JsonException;

namespace BlindTreasure.Application.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer connection)
    {
        _connection = connection;
        _database = connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;

        try
        {
            return JsonConvert.DeserializeObject<T>(value!);
        }
        catch (JsonException ex)
        {
            // Ghi log nếu cần
            Console.WriteLine($"[Redis] Deserialize error for key '{key}': {ex.Message}");
            return default;
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        var json = JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

        return await _database.StringSetAsync(key, json, expiration);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        return await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task<long> RemoveByPatternAsync(string pattern)
    {
        long deletedCount = 0;
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            var keys = server.Keys(pattern: $"{pattern}*").ToArray();
            if (keys.Any())
            {
                var result = await _database.KeyDeleteAsync(keys);
                deletedCount += result;
            }
        }
        return deletedCount;
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiration)
    {
        var json = JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

        return await _database.StringSetAsync(key, json, expiration, When.NotExists);
    }

    public async Task<bool> UpdateIfExistsAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var json = JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

        return await _database.StringSetAsync(key, json, expiration, When.Exists);
    }

    public async Task<long> ListAddAsync<T>(string key, T value, bool createIfNotExists = true)
    {
        var json = JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

        return await _database.ListRightPushAsync(key, json);
    }

    public async Task<IEnumerable<T>> ListRangeAsync<T>(string key, int start = 0, int stop = -1)
    {
        var values = await _database.ListRangeAsync(key, start, stop);
        var result = new List<T>();

        foreach (var value in values)
        {
            try
            {
                var item = JsonConvert.DeserializeObject<T>(value!);
                if (item != null)
                    result.Add(item);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Redis] Deserialize error in list for key '{key}': {ex.Message}");
            }
        }

        return result;
    }

    public async Task<IEnumerable<string>> FindKeysByPatternAsync(string pattern)
    {
        var keys = new List<string>();
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            var serverKeys = server.Keys(pattern: pattern).Select(k => (string)k);
            keys.AddRange(serverKeys);
        }
        return await Task.FromResult(keys);
    }
}