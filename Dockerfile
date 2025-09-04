FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore

RUN dotnet build -c Release --no-restore

WORKDIR /app/HealthScore.Processor
RUN dotnet publish -c Release -o out --no-build  --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

WORKDIR /app
COPY --from=build /app/HealthScore.Processor/out ./HealthScore.Processor

ENTRYPOINT ["dotnet", "HealthScore.Processor/HealthScore.Processor.dll"]