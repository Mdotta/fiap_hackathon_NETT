FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Solidary.sln .
COPY src/Solidary.Worker/Solidary.Worker.csproj src/Solidary.Worker/
COPY src/Solidary.Domain/Solidary.Domain.csproj src/Solidary.Domain/
COPY src/Solidary.Contracts/Solidary.Contracts.csproj src/Solidary.Contracts/
COPY src/Solidary.Infrastructure/Solidary.Infrastructure.csproj src/Solidary.Infrastructure/
RUN dotnet restore src/Solidary.Worker/Solidary.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Solidary.Worker/Solidary.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8081
ENTRYPOINT ["dotnet", "Solidary.Worker.dll"]
