FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/PartnerIntegration.Api/PartnerIntegration.Api.csproj src/PartnerIntegration.Api/
COPY src/PartnerIntegration.Application/PartnerIntegration.Application.csproj src/PartnerIntegration.Application/
COPY src/PartnerIntegration.Domain/PartnerIntegration.Domain.csproj src/PartnerIntegration.Domain/
COPY src/PartnerIntegration.Infrastructure/PartnerIntegration.Infrastructure.csproj src/PartnerIntegration.Infrastructure/
RUN dotnet restore src/PartnerIntegration.Api/PartnerIntegration.Api.csproj
COPY src/ src/
RUN dotnet publish src/PartnerIntegration.Api/PartnerIntegration.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PartnerIntegration.Api.dll"]
