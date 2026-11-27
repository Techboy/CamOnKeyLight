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
RUN apk add --no-cache wget
WORKDIR /app
COPY --from=build /app .
RUN chown -R 1654:1654 /app
USER 1654
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "CamOnKeyLight.Relay.dll"]
