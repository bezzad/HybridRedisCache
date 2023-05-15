using HybridRedisCache;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxConcurrentConnections = null;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = null;
    serverOptions.Limits.Http2.MaxStreamsPerConnection = int.MaxValue;
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.DisableStringReuse = true;
    serverOptions.AllowSynchronousIO = true;
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RedisCacheSystem API Sample", Version = "v1" });
});

builder.Services.AddHybridRedisCaching(options =>
{
    options.RedisConnectString = builder.Configuration["RedisConnection"];
    options.InstancesSharedName = "RedisCacheSystem.Sample.WebAPI";
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(120);
    options.DefaultDistributedExpirationTime = TimeSpan.FromDays(10);
    options.ThrowIfDistributedCacheError = false;
    options.ConnectRetry = int.MaxValue;
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
app.MapControllers();
app.Run();

// How to reset OS ports reservations
// Run CMD as Admin and execute below codes:
// $ net stop winnat
// $ net start winnat