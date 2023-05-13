using HybridRedisCache;
using Microsoft.AspNetCore.Mvc;

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

    [HttpGet]
    public async Task<WeatherForecast[]> GetAll([FromQuery] int count = 100)
    {
        Counter++;
        var cacheData = GetKeyValues(count);
        if (cacheData.Any())
        {
            return cacheData;
        }

        var newData = Enumerable.Range(1, 100).Select(index => new WeatherForecast
        {
            Id = index,
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();

        await Save(newData).ConfigureAwait(false);
        return newData;
    }

    [HttpGet("{id}")]
    public WeatherForecast Get(int id)
    {
        _cacheService.TryGetValue<WeatherForecast>(GetKey(id), out var filteredData);
        return filteredData;
    }

    [HttpPost]
    public async Task<WeatherForecast[]> Post(WeatherForecast[] values)
    {
        await Save(values).ConfigureAwait(false);
        return values;
    }

    [HttpPut]
    public async Task<WeatherForecast> Put(WeatherForecast value)
    {
        await Save(value).ConfigureAwait(false);
        return value;
    }

    [HttpPatch]
    public async void Patch(WeatherForecast WeatherForecast)
    {
        await Save(WeatherForecast).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    public async Task Delete(int id)
    {
        await _cacheService.RemoveAsync(GetKey(id)).ConfigureAwait(false);
    }

    [HttpDelete("ClearAll")]
    public async Task Delete()
    {
        await _cacheService.ClearAllAsync();
    }

    private async Task<bool> Save(params WeatherForecast[] weatherForecasts)
    {
        var expirationTime = TimeSpan.FromMinutes(50);
        await Parallel.ForEachAsync(weatherForecasts, async (weather, ct) =>
            await _cacheService.SetAsync(GetKey(weather.Id), weather, expirationTime).ConfigureAwait(false))
                .ConfigureAwait(false);

        return true;
    }

    private WeatherForecast[] GetKeyValues(int count = 100)
    {
        var result = new List<WeatherForecast>();
        for (int i = 0; i < count; i++)
            if (_cacheService.TryGetValue<WeatherForecast>(GetKey(i), out var data))
                result.Add(data);

        return result.ToArray();
    }

    private string GetKey(int id)
    {
        return $"{nameof(WeatherForecast)}_{id}";
    }
}