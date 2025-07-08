# ServiceControl.Connector.MassTransit

A ServiceControl container image that adds support for processing [MassTransit](https://masstransit.io/) failures with the Particular Platform. Making all its recoverability feature for managing message processing failures like (group) retrying, message editing and failed message inspection available to the MassTransit community.

## Installation

The connector is container image which is **Linux Arm64** and **Linux Amd64** compatible. The image is available at <https://hub.docker.com/r/particular/servicecontrol-masstransit-connector> . Please read the docker hub README for more information on available tags and container usage.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) -- Build connector solution
- [Docker](https://www.docker.com/) -- Build docker image

## Building

> [!NOTE]
> All snippets below assume these are launches from the root of the repo

### Build

```shell
dotnet build src/ServiceControl.Connector.MassTransit.sln
```

### Local container creation

> [!NOTE]
> The following creates a multiplatform images. This can only be build when the "Use containerd for pulling and storing images" is enabled under General.

> [!NOTE]
> The dockerfile is also compatible with <https://podman.io/>, replace `docker buildx build` with `podman build`.

To locally build and test the container run the following in any shell:

```shell
docker buildx build \
  --file src/ServiceControl.Connector.MassTransit.Host/Dockerfile \
  --platform linux/arm64,linux/amd64 \
  --tag particular/servicecontrol-masstransit-connector:latest .
```
### Troubleshooting

If you encounter the formatting issues error `IDE0055: Fix formatting` try removing the .editorconfig files from the repo folder

#### On cmd shell

```shell
dir /s/b ".editorconfig"
del /s ".editorconfig"
```

#### On Mac/Linux shell

```shell
rm .editorconfig -r
```
