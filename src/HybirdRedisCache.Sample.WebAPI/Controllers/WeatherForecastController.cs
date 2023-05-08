using HybridRedisCache;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;

namespace HybirdRedisCache.Sample.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static int Counter = 0;

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool",
        "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;
    private readonly IHybridCache _cacheService;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, IHybridCache cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        Counter++;
        var cacheData = GetKeyValues();
        if (cacheData.Any())
        {
            return cacheData.Values;
        }

        var newData = Enumerable.Range(1, 10).Select(index => new WeatherForecast
        {
            Id = index,
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();

        await Save(newData, 50).ConfigureAwait(false);
        return newData;
    }

    [HttpGet(nameof(WeatherForecast))]
    public WeatherForecast Get(int id)
    {
        var cacheData = GetKeyValues();
        cacheData.TryGetValue(id, out var filteredData);

        return filteredData;
    }

    [HttpPost("addWeatherForecasts/{durationMinutes}")]
    public async Task<WeatherForecast[]> PostList(WeatherForecast[] values, int durationMinutes)
    {
        var cacheData = GetKeyValues();
        foreach (var value in values)
        {
            cacheData[value.Id] = value;
        }
        await Save(cacheData.Values, durationMinutes).ConfigureAwait(false);
        return values;
    }

    [HttpPost("addWeatherForecast")]
    public async Task<WeatherForecast> Post(WeatherForecast value)
    {
        var cacheData = GetKeyValues();
        cacheData[value.Id] = value;
        await Save(cacheData.Values).ConfigureAwait(false);
        return value;
    }

    [HttpPut("updateWeatherForecast")]
    public async void Put(WeatherForecast WeatherForecast)
    {
        var cacheData = GetKeyValues();
        cacheData[WeatherForecast.Id] = WeatherForecast;
        await Save(cacheData.Values).ConfigureAwait(false);
    }

    [HttpDelete("deleteWeatherForecast")]
    public async Task Delete(int id)
    {
        var cacheData = GetKeyValues();
        cacheData.Remove(id);
        await Save(cacheData.Values).ConfigureAwait(false);
    }

    [HttpDelete("ClearAll")]
    public async Task Delete()
    {
        await _cacheService.ClearAllAsync();
    }

    private async Task<bool> Save(IEnumerable<WeatherForecast> weatherForecasts, double expireAfterMinutes = 50)
    {
        var expirationTime = TimeSpan.FromMinutes(expireAfterMinutes);
        await _cacheService.SetAsync(nameof(WeatherForecast), weatherForecasts, expirationTime);
        return true;
    }

    private Dictionary<int, WeatherForecast> GetKeyValues()
    {
        var data = _cacheService.Get<IEnumerable<WeatherForecast>>(nameof(WeatherForecast));
        return data?.ToDictionary(key => key.Id, val => val) ?? new Dictionary<int, WeatherForecast>();
    }
}