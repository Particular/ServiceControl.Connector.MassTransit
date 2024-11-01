# Particular Software ServiceControl Masstransit connector

This document describes basic usage and information related `particular/servicecontrol-connector-masstransit` image. It is a component that is part of the Particular Platform. Complete documentation of the ServiceControl container images can be found in the [Particular Software ServiceControl documentation](https://docs.particular.net/servicecontrol).

## Image tagging

### `latest` tag

This tag is primarily for developers wanting to use the latest (non prerelease) version. If a release targets the current latest major or is a new major after the previous latest, then the `:latest` tag is applied to the image pushed to Docker Hub.

If the release is a patch release to a previous major, then the `:latest` tag will not be added.

### Version tags

We use [SemVer](http://semver.org/) for versioning. Release images pushed to Docker Hub will be tagged with the release version.

### Major version tag

The latest release within a major version will be tagged with the major version number only on images pushed to Docker Hub. This allows users to target a specific major version to help avoid the risk of incurring breaking changes between major versions.

### Minor version tag

The latest release within a minor version will be tagged with `{major}.{minor}` on images pushed to Docker Hub. This allows users to target the latest patch within a specific minor version.

## Image architecture

The multi-architecture image supports `linux/arm64` and `linux/amd64`.

## Usage

This is an extension to  ServiceControl. Usage information ServiceControl containers is available at: <https://hub.docker.com/r/particular/servicecontrol>

The following is the most basic way to create ServiceControl containers using [Docker](https://www.docker.com/), assuming a RabbitMQ message broker also hosted in a Docker container using default `guest`/`guest` credentials:

### Setup

Run with setup entry point to create message queues, then exit the container.

```shell
docker run \
-e TRANSPORTTYPE=RabbitMQ.QuorumConventionalRouting \
-e CONNECTIONSTRING=host=host.docker.internal \
-e MANAGEMENTAPI=http://guest:guest@host.docker.internal:15672 \
--rm particular/servicecontrol-connector-masstransit:latest \
--setup
```

### Run

Run the connector and bridge MassTransit errors queues with the Particular Platform.

```shell
docker run \
-e TRANSPORTTYPE=RabbitMQ.QuorumConventionalRouting \
-e CONNECTIONSTRING=host=host.docker.internal \
-e MANAGEMENTAPI=http://guest:guest@host.docker.internal:15672 \
--rm particular/servicecontrol-connector-masstransit:latest \
```

## Configuration

| Key              | Description                                                                                         | Default                                                  |
|------------------|-----------------------------------------------------------------------------------------------------|----------------------------------------------------------|
| TRANSPORTTYPE    | The transport type                                                                                  | None                                                     |
| CONNECTIONSTRING | The NServiceBus connection string for the specified transport                                       | None                                                     |
| RETURNQUEUE      | The intermediate queue used by the connector to which ServiceControl will send its retried messages | `Particular.ServiceControl.Connector.MassTransit_return` |
| ERRORQUEUE       | The error queue ServiceControl ingests                                                              | `error`                                                  |
| MANAGEMENTAPI    | RabbitMQ management API url when RabbitMQ is selected as transport                                  | None                                                     |
| QUEUES_FILE      | File that contains each error queue to monitor as a seperate line                                   | None                                                     |
| RECEIVEMODE      | Azure Service Bus: By default ingest `*_error` but can ingest from dead-letter queues               | `Queue`                                                  |

### TRANSPORTTYPE

Currently support as the most used MassTransit transports: Amazon SQS, Azure Service Bus and RabbitMQ.

| Description       | Key                                  |
|-------------------|--------------------------------------|
| Amazon SQS        | `AmazonSQS`                          |
| Azure Service Bus | `NetStandardAzureServiceBus`         |
| RabbitMQ          | `RabbitMQ.QuorumConventionalRouting` |

### CONNECTIONSTRING

The connection string format used is the same for all ServiceControl components.

- Azure Service Bus: <https://docs.particular.net/servicecontrol/transports#azure-service-bus>
- RabbitMQ: <https://docs.particular.net/servicecontrol/transports#rabbitmq>
- AmazonSQS: <https://docs.particular.net/servicecontrol/transports#amazon-sqs>

### RETURNQUEUE

Default: `Particular.ServiceControl.Connector.MassTransit_return`

The return queue used by the connector that is passed to ServiceControl as the intermediate queue before returning the message back to the actual queue that MassTransit listens to.

### ERRORQUEUE

Default: `error`

ServiceControl by default listens to the `error` queue but it this value is overriden in ServiceControl this configuration setting must be set to the same value.

### MANAGEMENTAPI

Default: None

> [!NOTE]
> Only applies to RabbitMQ

Required when using RabbitMQ and error queues need to be dynamically resolved as queue information is queried on the broker to determine which error queues to listen to. The url needs to contain the username and password used to authenticate.

Example:

```txt
http://guest:guest@localhost:15672
```

### QUEUES_FILE

Default: None

Path that contains a static list of queues. If no value is specified the connector will run in dynamic mode.

Example:

```txt
/queues.txt
```

### SUBQUEUE

Default: Queue

Values: None | DeadLetter

> [!NOTE]
> Only applies to Azure Service Bus

Failed message by default (mode `Queue`) will be ingested from queues matching `*_error` but by specifying `DeadLetter` the connector will ingest messages from the [Service Bus dead-letter (sub) queues](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues).

## Support

The MassTransit connector is currently in preivew and currently consider a "community extension". If you plan to use this in a production environment we would appreciate it if you let us know at <https://discuss.particular.net/>!

## Feedback

If you miss certain features or have any type of feedback then you can do this at:

- <https://github.com/Particular/ServiceControl.Connector.MassTransit>
- <https://discuss.particular.net/>

## Authors

This software, including this container image, is built and maintained by the team at Particular Software. See also the list of contributors who participated in this project.

## License

This project is licensed under the Reciprocal Public License 1.5 (RPL1.5) and commercial licenses are available - see the [source repository license file](https://github.com/Particular/ServiceControl/blob/master/LICENSE.md) for more information.
