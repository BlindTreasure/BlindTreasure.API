﻿using System.Text.Json;
using BlindTreasure.Application.Interfaces;
using StackExchange.Redis;

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
        return JsonSerializer.Deserialize<T>(value);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var json = JsonSerializer.Serialize(value);
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
}