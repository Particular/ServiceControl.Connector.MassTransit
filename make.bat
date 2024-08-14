git submodule init
git submodule update

docker buildx build --file src/ServiceControl.Connector.MassTransit.Host/Dockerfile --platform linux/arm64,linux/amd64 --tag particular/servicecontrol-connector-masstransit:latest .