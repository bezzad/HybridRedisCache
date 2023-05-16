#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["HybirdRedisCache.Sample.WebAPI/HybirdRedisCache.Sample.WebAPI.csproj", "HybirdRedisCache.Sample.WebAPI/"]
COPY ["HybridRedisCache/HybridRedisCache.csproj", "HybridRedisCache/"]
RUN dotnet restore "HybirdRedisCache.Sample.WebAPI/HybirdRedisCache.Sample.WebAPI.csproj"
COPY . .
WORKDIR "/src/HybirdRedisCache.Sample.WebAPI"
RUN dotnet build "HybirdRedisCache.Sample.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HybirdRedisCache.Sample.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HybirdRedisCache.Sample.WebAPI.dll"]