using HybridRedisCache;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
    options.RedisCacheConnectString = builder.Configuration["Redis"];
    options.InstancesSharedName = "RedisCacheSystem.Demo";
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(1);
    options.DefaultDistributedExpirationTime = TimeSpan.FromDays(10);
    options.ThrowIfDistributedCacheError = true;
    options.RedisCacheConnectString = "localhost:6379";
    options.BusRetryCount = 10;
    options.EnableLogging = true;
    options.FlushLocalCacheOnBusReconnection = true;
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
