using HybridRedisCache;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace HybirdRedisCache.Sample.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
[DisableCors]
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

    [HttpGet]
    public async Task<WeatherForecast[]> GetAll([FromQuery] int count = 100)
    {
        _logger.LogInformation($"GET All: WeatherForecast[count: {count}]");
        var cacheData = GetKeyValues(count);
        if (cacheData.Any())
        {
            return cacheData;
        }

        var newData = Enumerable.Range(1, 100).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();

        foreach (var data in newData)
            await Save(data).ConfigureAwait(false);

        return newData;
    }

    [HttpGet("{id}")]
    public Task<WeatherForecast> Get(string id)
    {
        _logger.LogInformation($"GET: WeatherForecast {{ id: {id} }}");
        _cacheService.TryGetValue<WeatherForecast>(GetKey(id), out var filteredData);
        return Task.FromResult(filteredData);
    }

    [HttpPost]
    public async Task Post(WeatherForecast value)
    {
        _logger.LogInformation($"Post: WeatherForecast {{ id: {value.Id} }}");
        await Save(value).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        _logger.LogInformation($"Delete: WeatherForecast {{ id: {id} }}");
        await _cacheService.RemoveAsync(GetKey(id)).ConfigureAwait(false);
    }

    [HttpDelete("ClearAll")]
    public async Task Delete()
    {
        _logger.LogInformation($"Delete: all WeatherForecast");
        await _cacheService.ClearAllAsync();
    }

    private async Task<bool> Save(WeatherForecast weather)
    {
        var expirationTime = TimeSpan.FromMinutes(120);
        await _cacheService.SetAsync(GetKey(weather.Id), weather, expirationTime, fireAndForget: false).ConfigureAwait(false);
        return true;
    }

    private WeatherForecast[] GetKeyValues(int count = 100)
    {
        var result = new List<WeatherForecast>();
        for (int i = 0; i < count; i++)
            if (_cacheService.TryGetValue<WeatherForecast>(GetKey(i.ToString()), out var data))
                result.Add(data);

        return result.ToArray();
    }

    private string GetKey(string id)
    {
        return $"{nameof(WeatherForecast)}_{id}";
    }
}