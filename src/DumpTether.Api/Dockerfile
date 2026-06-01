FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/DumpTether.Domain/DumpTether.Domain.csproj src/DumpTether.Domain/
COPY src/DumpTether.App/DumpTether.App.csproj src/DumpTether.App/
COPY src/DumpTether.Data/DumpTether.Data.csproj src/DumpTether.Data/
COPY src/DumpTether.Api/DumpTether.Api.csproj src/DumpTether.Api/
RUN dotnet restore src/DumpTether.Api/DumpTether.Api.csproj

COPY src/ src/
RUN dotnet publish src/DumpTether.Api/DumpTether.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DumpTether.Api.dll"]
