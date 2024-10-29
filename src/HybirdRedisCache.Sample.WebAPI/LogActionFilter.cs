using Microsoft.AspNetCore.Mvc.Filters;
using Prometheus;

namespace HybirdRedisCache.Sample.WebAPI;

public class LogActionFilter : IActionFilter
{
    private static readonly Counter ProcessedRequestCount = Metrics.CreateCounter("HybirdRedisCache_requests_processed_total", "Number of processed requests.");
    private static readonly Gauge RequestsInQueue = Metrics.CreateGauge("HybirdRedisCache_requests_queued", "Number of requests waiting for processing in the queue.");
    private static readonly Histogram ResponseLatency = Metrics.CreateHistogram(
        "HybirdRedisCache_response_latency_ms",
        "Histogram of request call processing durations.",
                new HistogramConfiguration
                {
                    // We divide measurements in 10 buckets of 0ms each, up to 1000.
                    Buckets = Histogram.LinearBuckets(start: 0, width: 100, count: 10),
                    ExemplarBehavior = new()
                    {
                        DefaultExemplarProvider = RecordExemplarForSlowRecordProcessingDuration,
                        // Even if we have interesting data more often, do not record it to conserve exemplar storage.
                        NewExemplarMinInterval = TimeSpan.FromMinutes(5)
                    }
                });

    // For the next histogram we only want to record exemplars for values larger than 0.1 (i.e. when record processing goes slowly).
    static Exemplar RecordExemplarForSlowRecordProcessingDuration(Collector metric, double value)
    {
        if (value < 0.1)
            return Exemplar.None;

        return Exemplar.FromTraceContext();
    }

    private readonly ILoggerFactory _loggerFactory;
    private Prometheus.ITimer _timer;


    public LogActionFilter(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        ProcessedRequestCount.Inc();
        _timer = ResponseLatency.NewTimer();
        RequestsInQueue.Inc();

        // Log information before every action method is called
        var controllerName = context.RouteData.Values["controller"].ToString();
        //var actionName = context.RouteData.Values["action"].ToString();
        var logger = _loggerFactory.CreateLogger(controllerName);
        var request = context.HttpContext.Request;
        logger.LogInformation($"[{ProcessedRequestCount.Value}] | {request.Protocol} {request.Method} {request.Scheme}://{request.Host}/{request.Path}");
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        RequestsInQueue.Dec();
        _timer?.Dispose();
    }
}
