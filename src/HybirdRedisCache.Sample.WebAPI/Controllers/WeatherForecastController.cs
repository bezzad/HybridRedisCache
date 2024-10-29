using HybridRedisCache;
using Microsoft.AspNetCore.Mvc;

namespace HybirdRedisCache.Sample.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
[ServiceFilter(typeof(LogActionFilter))]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool",
        "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };
    private readonly IHybridCache _cacheService;

    public WeatherForecastController(IHybridCache cacheService)
    {
        _cacheService = cacheService;
    }

    [HttpGet("{id}")]
    public Task<WeatherForecast> Get(string id)
    {
        return _cacheService.GetAsync<WeatherForecast>(GetKey(id));
    }

    [HttpPost]
    public async Task Post(WeatherForecast value)
    {
        await Save(value).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        await _cacheService.RemoveAsync(GetKey(id)).ConfigureAwait(false);
    }

    [HttpDelete("ClearAll")]
    public async Task Delete()
    {
        await _cacheService.ClearAllAsync().ConfigureAwait(false);
    }

    private async Task<bool> Save(WeatherForecast weather)
    {
        var expirationTime = TimeSpan.FromMinutes(120);
        await _cacheService.SetAsync(GetKey(weather.Id), weather, expirationTime).ConfigureAwait(false);
        return true;
    }

    private string GetKey(string id)
    {
        return $"{nameof(WeatherForecast)}_{id}";
    }
}