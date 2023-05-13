start cmd /c "dotnet run -p ""..\HybirdRedisCache.Sample.WebAPI\HybirdRedisCache.Sample.WebAPI.csproj"" --configuration Release"
timeout /t 5 /nobreak
ping -n 4 127.0.0.1 > nul
k6 run app.js
# docker run --rm -i grafana/k6 run - <app.js
pause