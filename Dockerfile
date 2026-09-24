FROM mcr.microsoft.com/dotnet/sdk:8.0.424-bookworm-slim AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/JobFree.Domain/JobFree.Domain.csproj src/JobFree.Domain/
COPY src/JobFree.Application/JobFree.Application.csproj src/JobFree.Application/
COPY src/JobFree.Infrastructure/JobFree.Infrastructure.csproj src/JobFree.Infrastructure/
COPY src/JobFree.Api/JobFree.Api.csproj src/JobFree.Api/
RUN dotnet restore src/JobFree.Api/JobFree.Api.csproj
COPY src/ src/
RUN dotnet publish src/JobFree.Api/JobFree.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "JobFree.Api.dll"]
