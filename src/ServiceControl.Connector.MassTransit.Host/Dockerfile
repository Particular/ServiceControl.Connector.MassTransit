# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
COPY . /import
WORKDIR /import
ENV CI=true

RUN dotnet publish src/ServiceControl.Connector.MassTransit.Host/ServiceControl.Connector.MassTransit.Host.csproj --configuration Release --arch $TARGETARCH --output /build

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled
WORKDIR /app

COPY --from=build ./build /app

ENV QUEUES_FILE="/app/queues.txt"
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
HEALTHCHECK --start-period=20s CMD ["/app/ServiceControl.Connector.MassTransit.Host", "health-check"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Connector.MassTransit.Host"]