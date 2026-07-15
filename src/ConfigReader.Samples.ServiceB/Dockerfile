# Build context is the solution root (code/). See docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ConfigReader.Samples.ServiceB/ConfigReader.Samples.ServiceB.csproj
RUN dotnet publish src/ConfigReader.Samples.ServiceB/ConfigReader.Samples.ServiceB.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "ConfigReader.Samples.ServiceB.dll"]
