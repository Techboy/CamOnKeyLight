# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine AS build
WORKDIR /src
COPY CamOnKeyLight.Core/CamOnKeyLight.Core.csproj CamOnKeyLight.Core/
COPY relay/CamOnKeyLight.Relay.csproj relay/
RUN dotnet restore relay/CamOnKeyLight.Relay.csproj
COPY CamOnKeyLight.Core/ CamOnKeyLight.Core/
COPY relay/ relay/
RUN dotnet publish relay/CamOnKeyLight.Relay.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine AS runtime
USER root
WORKDIR /app
COPY --from=build --chown=1654:1654 /app .
USER 1654
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
EXPOSE 8080
# dotnet is on PATH in the ASP.NET runtime image. The probe reads
# ASPNETCORE_URLS, then HTTP_PORTS / ASPNETCORE_HTTP_PORTS, so a custom port stays healthy.
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["dotnet", "CamOnKeyLight.Relay.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "CamOnKeyLight.Relay.dll"]
