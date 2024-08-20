# ServiceControl.Connector.MassTransit

A ServiceControl container image that adds support for processing [MassTransit](https://masstransit.io/) failures with the Particular Platform. Making all its recoverability feature for managing message processing failures like (group) retrying, message editing and failed message inspection available to the MassTransit community.

## Installation

The connector is container image which is Linux Arm64 and Linux Amd64 compatible. The image is available at https://hub.docker.com/r/particular/servicecontrol-connector-masstransit . Please read the docker hub README for more information on available tags and container usage.

## Local container creation

To locally build and test the container run the following in any shell:

```shell
git submodule init
git submodule update
docker buildx build --file src/ServiceControl.Connector.MassTransit.Host/Dockerfile --platform linux/arm64,linux/amd64 --tag particular/servicecontrol-connector-masstransit:latest .
```
