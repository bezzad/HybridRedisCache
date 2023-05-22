using HybirdRedisCache.Sample.WebAPI;
using HybridRedisCache;
using Microsoft.OpenApi.Models;
using Prometheus;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxConcurrentConnections = null;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = null;
    serverOptions.Limits.Http2.MaxStreamsPerConnection = int.MaxValue;
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(120);
    serverOptions.DisableStringReuse = true;
    serverOptions.AllowSynchronousIO = true;
});

// Add services to the container.

builder.Services.AddScoped<LogActionFilter>();
builder.Services.AddControllers();
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

// open http://localhost:9091/metrics in web browser to look metrics data
builder.Services.AddMetricServer(opt =>
{
    opt.Port = 9091;
    opt.Hostname = "localhost";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Capture metrics about all received HTTP requests.
app.UseHttpMetrics(opt =>
{
    opt.ConfigureMeasurements(measurementOptions =>
    {
        // Only measure exemplar if the HTTP response status code is not "OK".
        measurementOptions.ExemplarPredicate = context => context.Response.StatusCode != (int)HttpStatusCode.OK;
    });
}); 
app.MapControllers();
app.Run();

// How to reset OS ports reservations
// Run CMD as Admin and execute below codes:
// $ net stop winnat
// $ net start winnat