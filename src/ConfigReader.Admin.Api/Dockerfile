# Build context is the solution root (code/). See docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ConfigReader.Admin.Api/ConfigReader.Admin.Api.csproj
RUN dotnet publish src/ConfigReader.Admin.Api/ConfigReader.Admin.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConfigReader.Admin.Api.dll"]
