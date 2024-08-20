# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .

RUN dotnet publish --os linux --arch $TARGETARCH src/ServiceControl.Connector.MassTransit.Host/ServiceControl.Connector.MassTransit.Host.csproj --configuration Release -graph --output ./build

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
ARG VERSION
WORKDIR /app

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl.Connector.MassTransit \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://github.com/Particular/ServiceControl.Connector.MassTransit \
      org.opencontainers.image.documentation=https://github.com/Particular/ServiceControl.Connector.MassTransit \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.Connector.MassTransit \
      org.opencontainers.image.description="ServiceControl MassTransit connector"

COPY --from=build ./build /app

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Connector.MassTransit.Host"]