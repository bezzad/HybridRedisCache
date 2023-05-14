namespace HybirdRedisCache.Sample.WebAPI;

public class WeatherForecast
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    public string Summary { get; set; }
}