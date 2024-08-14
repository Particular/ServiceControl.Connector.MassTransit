# servicecontrol-connector-masstransit
# docker buildx build --push --tag ghcr.io/particular/servicecontrol-connector-masstransit:${{ env.TAG_NAME }} \
#     --file src/${{ matrix.project }}/Dockerfile \
#     --build-arg VERSION=${{ env.MinVerVersion }} \
#     --annotation "index:org.opencontainers.image.title=${{ matrix.name }}" \
#     --annotation "index:org.opencontainers.image.description=${{ matrix.description }}" \
#     --annotation "index:org.opencontainers.image.created=$(date '+%FT%TZ')" \
#     --annotation "index:org.opencontainers.image.revision=${{ github.sha }}" \
#     --annotation "index:org.opencontainers.image.authors=Particular Software" \
#     --annotation "index:org.opencontainers.image.vendor=Particular Software" \
#     --annotation "index:org.opencontainers.image.version=${{ env.MinVerVersion }}" \
#     --annotation "index:org.opencontainers.image.source=https://github.com/${{ github.repository }}/tree/${{ github.sha }}" \
#     --annotation "index:org.opencontainers.image.url=https://hub.docker.com/r/particular/${{ matrix.name }}" \
#     --annotation "index:org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/" \
#     --annotation "index:org.opencontainers.image.base.name=mcr.microsoft.com/dotnet/aspnet:8.0" \
#     --platform linux/arm64,linux/amd64 .

#docker buildx imagetools inspect ghcr.io/particular/${{ matrix.name }}:${{ env.TAG_NAME }}

docker buildx build --file src/ServiceControl.Connector.MassTransit.Host/Dockerfile --build-arg VERSION=0.1.0-alpha.0.1 --platform linux/arm64,linux/amd64 .