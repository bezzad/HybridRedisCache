namespace HybirdRedisCache.Sample.WebAPI;

public class WeatherForecast
{
    public int Id { get; set; } = DateTime.Now.GetHashCode();

    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    public string Summary { get; set; }
}