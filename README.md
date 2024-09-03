# ServiceControl.Connector.MassTransit

A ServiceControl container image that adds support for processing [MassTransit](https://masstransit.io/) failures with the Particular Platform. Making all its recoverability feature for managing message processing failures like (group) retrying, message editing and failed message inspection available to the MassTransit community.

## Installation

The connector is container image which is **Linux Arm64** and **Linux Amd64** compatible. The image is available at https://hub.docker.com/r/particular/servicecontrol-connector-masstransit . Please read the docker hub README for more information on available tags and container usage.

## Prerequisites

- [Git](https://git-scm.com/) -- Init git submodules
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) -- Build connector solution
- [Docker](https://www.docker.com/) -- Build docker image

## Building

> [!NOTE]
> All snippets below assume these are launches from the root of the repo

### Init git submodules

The connector requires some changes to the transports that do not yet exist on the transport packages. To version these changes we use git submodules which need to be initialized in order for the solution to be build.

```shell
git submodule init
git submodule update
```

### Build

```shell
git submodule init
git submodule update
dotnet build src/ServiceControl.Connector.MassTransit.sln
```

### Local container creation

To locally build and test the container run the following in any shell:

```shell
git submodule init
git submodule update
docker buildx build --file src/ServiceControl.Connector.MassTransit.Host/Dockerfile --platform linux/arm64,linux/amd64 --tag particular/servicecontrol-connector-masstransit:latest .
```
