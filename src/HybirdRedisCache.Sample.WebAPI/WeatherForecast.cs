using System.Text.Json.Serialization;

namespace HybirdRedisCache.Sample.WebAPI;

public class WeatherForecast
{
    public int Id { get; set; } = DateTime.Now.GetHashCode();

    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    [JsonIgnore]
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string Summary { get; set; }
}