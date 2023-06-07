using HybirdRedisCache.Sample.WebAPI;
using HybridRedisCache;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;

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

// ---------- Add Serilog Logger -----------------------
var outPutTemplate = "[{Timestamp:HH:mm:ss} {Level:u3} {ClientIp} {ClientAgent} {MachineName} {EnvironmentName} {CorrelationId} {GlobalLogContext} {UserName}{NewLine} {RequestPath} {Input}]{NewLine} {Message:lj}{NewLine}{Exception}";
var logger = new LoggerConfiguration()
    //.ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: outPutTemplate)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithClientIp()
    .Enrich.WithClientAgent()
    .Enrich.WithEnvironmentName()
    .Enrich.WithCorrelationId()
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);
// -----------------------------------------------------

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

// open http://localhost:5000/metrics in web browser to look metrics data
builder.Services.AddMetricServer(opt =>
{
    opt.Port = 5000;
    opt.Hostname = "localhost";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpMetrics(); // Capture metrics about all received HTTP requests.
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
    endpoints.MapControllers();
});
app.Run();

// How to reset OS ports reservations
// Run CMD as Admin and execute below codes:
// $ net stop winnat
// $ net start winnat