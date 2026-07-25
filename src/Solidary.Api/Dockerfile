FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Solidary.sln .
COPY src/Solidary.Api/Solidary.Api.csproj src/Solidary.Api/
COPY src/Solidary.Application/Solidary.Application.csproj src/Solidary.Application/
COPY src/Solidary.Domain/Solidary.Domain.csproj src/Solidary.Domain/
COPY src/Solidary.Contracts/Solidary.Contracts.csproj src/Solidary.Contracts/
COPY src/Solidary.Infrastructure/Solidary.Infrastructure.csproj src/Solidary.Infrastructure/
RUN dotnet restore src/Solidary.Api/Solidary.Api.csproj

COPY src/ src/
RUN dotnet publish src/Solidary.Api/Solidary.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Solidary.Api.dll"]
