using HybridRedisCache;
using Microsoft.AspNetCore.Mvc;

namespace HybirdRedisCache.Sample.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
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

    [HttpGet("{id}")]
    public Task<WeatherForecast> Get(string id)
    {
        //_logger.LogInformation($"GET: WeatherForecast {{ id: {id} }}");
        return _cacheService.GetAsync<WeatherForecast>(GetKey(id));
    }

    [HttpPost]
    public async Task Post(WeatherForecast value)
    {
        //_logger.LogInformation($"Post: WeatherForecast {{ id: {value.Id} }}");
        await Save(value).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        //_logger.LogInformation($"Delete: WeatherForecast {{ id: {id} }}");
        await _cacheService.RemoveAsync(GetKey(id)).ConfigureAwait(false);
    }

    [HttpDelete("ClearAll")]
    public async Task Delete()
    {
        //_logger.LogInformation($"Delete: all WeatherForecast");
        await _cacheService.ClearAllAsync().ConfigureAwait(false);
    }

    private async Task<bool> Save(WeatherForecast weather)
    {
        var expirationTime = TimeSpan.FromMinutes(120);
        await _cacheService.SetAsync(GetKey(weather.Id), weather, expirationTime, fireAndForget: false).ConfigureAwait(false);
        return true;
    }

    private string GetKey(string id)
    {
        return $"{nameof(WeatherForecast)}_{id}";
    }
}