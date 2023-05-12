# docker run --rm -i grafana/k6 run - <app.js
dotnet run "../HybridRedisCache.Sample.WebAPI/"
k6 run app.js
pause