# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
COPY . /import
WORKDIR /import
ENV CI=true

RUN dotnet publish src/ServiceControl.Connector.MassTransit.Host/ServiceControl.Connector.MassTransit.Host.csproj --configuration Release --arch $TARGETARCH --output /build

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled
ARG VERSION=0.0.1
ARG SHA=unknown
ARG CREATED=2000-01-01T00:00:00Z
WORKDIR /app

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl.Connector.MassTransit/tree/$SHA
LABEL org.opencontainers.image.authors="Particular Software"
LABEL org.opencontainers.image.vendor="Particular Software"
LABEL org.opencontainers.image.url=https://hub.docker.com/r/particular/servicecontrol-masstransit-connector
LABEL org.opencontainers.image.documentation=https://hub.docker.com/r/particular/servicecontrol-masstransit-connector
LABEL org.opencontainers.image.version=$VERSION
LABEL org.opencontainers.image.revision=$SHA
LABEL org.opencontainers.image.created=$CREATED
LABEL org.opencontainers.image.title="MassTransit Connector for ServiceControl"
LABEL org.opencontainers.image.description="An extension to ServiceControl that adds support for processing MassTransit failures with the Particular Platform."
LABEL org.opencontainers.image.base.name=mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled

COPY --from=build ./build /app

ENV QUEUES_FILE="/app/queues.txt"
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Connector.MassTransit.Host"]