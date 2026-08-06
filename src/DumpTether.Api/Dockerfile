FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/DumpTether.Domain/DumpTether.Domain.csproj src/DumpTether.Domain/
COPY src/DumpTether.App/DumpTether.App.csproj src/DumpTether.App/
COPY src/DumpTether.Data/DumpTether.Data.csproj src/DumpTether.Data/
COPY src/DumpTether.Data.Sqlite/DumpTether.Data.Sqlite.csproj src/DumpTether.Data.Sqlite/
COPY src/DumpTether.Api/DumpTether.Api.csproj src/DumpTether.Api/
COPY src/DumpTether.Admin/DumpTether.Admin.csproj src/DumpTether.Admin/
RUN dotnet restore src/DumpTether.Api/DumpTether.Api.csproj \
    && dotnet restore src/DumpTether.Admin/DumpTether.Admin.csproj

COPY src/ src/
RUN dotnet publish src/DumpTether.Api/DumpTether.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    && dotnet publish src/DumpTether.Admin/DumpTether.Admin.csproj \
    --configuration Release \
    --output /app/admin \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG IMAGE_VERSION=development
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.title="DumpTether API" \
      org.opencontainers.image.description="DumpTether ASP.NET Core API and SignalR host" \
      org.opencontainers.image.source="https://github.com/bheldbo/DumpTether" \
      org.opencontainers.image.url="https://dumptether.bheldbo.dk" \
      org.opencontainers.image.documentation="https://github.com/bheldbo/DumpTether/blob/main/README.md" \
      org.opencontainers.image.licenses="NOASSERTION" \
      org.opencontainers.image.vendor="bheldbo" \
      org.opencontainers.image.version="${IMAGE_VERSION}" \
      org.opencontainers.image.revision="${SOURCE_REVISION}"
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet
COPY --from=build --chown=app:app /app/publish .
COPY --from=build --chown=app:app /app/admin /tools/admin
USER app
ENTRYPOINT ["dotnet", "DumpTether.Api.dll"]
