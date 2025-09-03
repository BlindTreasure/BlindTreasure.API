using BlindTreasure.Application.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;
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

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var json = JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

        await _database.StringSetAsync(key, json, expiration);
    }

    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            var keys = server.Keys(pattern: $"{pattern}*").ToArray();
            if (keys.Any())
                await _database.KeyDeleteAsync(keys);
        }
    }

    public async Task ClearAllAppCachesAsync()
    {
        //xóa het me luon 
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            var keys = server.Keys(database: _database.Database).ToArray();

            if (keys.Any())
            {
                await _database.KeyDeleteAsync(keys);
            }
        }
        //await RemoveByPatternAsync("user:");
        //await RemoveByPatternAsync("seller:");
        //await RemoveByPatternAsync("product:");
        //await RemoveByPatternAsync("category:");
        //await RemoveByPatternAsync("blindbox:");
        //await RemoveByPatternAsync("gemini:");
        //await RemoveByPatternAsync("address:");
        //await RemoveByPatternAsync("inventoryitem:");
        //await RemoveByPatternAsync("listings:");
        //await RemoveByPatternAsync("listing:");
        //await RemoveByPatternAsync("Promotion:");
        //await RemoveByPatternAsync("refresh:");
        //await RemoveByPatternAsync("otp-sent:");
        //await RemoveByPatternAsync("otp:");
        //await RemoveByPatternAsync("counter:");
        //await RemoveByPatternAsync("order:");
        //await RemoveByPatternAsync("noti:");
        //await RemoveByPatternAsync("ParticipantPromotion:");
        //await RemoveByPatternAsync("chat:");
        //await RemoveByPatternAsync("user_online:");

    }



}