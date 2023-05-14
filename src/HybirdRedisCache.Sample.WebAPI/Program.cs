using HybridRedisCache;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = long.MaxValue;
    options.Limits.MaxConcurrentUpgradedConnections = long.MaxValue;
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RedisCacheSystem", Version = "v1" });
});

builder.Services.AddHybridRedisCaching(options =>
{
    options.RedisConnectString = builder.Configuration["RedisConnection"];
    options.InstancesSharedName = "RedisCacheSystem.Sample.WebAPI";
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(120);
    options.DefaultDistributedExpirationTime = TimeSpan.FromDays(10);
    options.ThrowIfDistributedCacheError = true;
    options.ConnectRetry = 10;
    options.EnableLogging = true;
    options.FlushLocalCacheOnBusReconnection = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();