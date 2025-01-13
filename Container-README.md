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
-e TRANSPORT_TYPE=RabbitMQ \
-e CONNECTION_STRING=host=host.docker.internal \
-e RABBITMQ_MANAGEMENT_API_URL=http://host.docker.internal:15672 \
-e RABBITMQ_MANAGEMENT_API_USERNAME=guest \
-e RABBITMQ_MANAGEMENT_API_PASSWORD=guest \
--rm particular/servicecontrol-connector-masstransit:latest \
--setup
```

### Run

Run the connector and bridge MassTransit errors queues with the Particular Platform.

```shell
docker run \
-e TRANSPORT_TYPE=RabbitMQ \
-e CONNECTION_STRING=host=host.docker.internal \
-e RABBITMQ_MANAGEMENT_API_URL=http://host.docker.internal:15672 \
-e RABBITMQ_MANAGEMENT_API_USERNAME=guest \
-e RABBITMQ_MANAGEMENT_API_PASSWORD=guest \
-v [local_path_to_queues_file]:/app/queues.txt:ro \
--rm particular/servicecontrol-connector-masstransit:latest
```

## Configuration

| Key                              | Description                                                                                         | Default                                                  |
|----------------------------------|-----------------------------------------------------------------------------------------------------|----------------------------------------------------------|
| TRANSPORT_TYPE                   | The transport type                                                                                  | None                                                     |
| CONNECTION_STRING                | The NServiceBus connection string for the specified transport                                       | None                                                     |
| RETURN_QUEUE                     | The intermediate queue used by the connector to which ServiceControl will send its retried messages | `Particular.ServiceControl.Connector.MassTransit_return` |
| ERROR_QUEUE                      | The error queue ServiceControl ingests                                                              | `error`                                                  |
| CUSTOM_CHECK_QUEUE               | The ServiceControl endpoint queue                                                                   | `Particular.ServiceControl`                                                  |
| RABBITMQ_MANAGEMENT_API_URL      | RabbitMQ management API url when RabbitMQ is selected as transport                                  | None                                                     |
| RABBITMQ_MANAGEMENT_API_USERNAME | RabbitMQ management API username                                                                    | `guest`                                                  |
| RABBITMQ_MANAGEMENT_API_PASSWORD | RabbitMQ management API password                                                                    | `guest`                                                  |

### TRANSPORT_TYPE

Currently support as the most used MassTransit transports: Amazon SQS, Azure Service Bus and RabbitMQ.

| Description       | Key                                  | Notes |
|-------------------|--------------------------------------| --- |
| Amazon SQS        | `AmazonSQS`                          | |
| Azure Service Bus | `AzureServiceBus`         | |
| Azure Service Bus with Dead Letter | `AzureServiceBusDeadLetter`         | Azure Service Bus configured to ingest from [dead-letter queues](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues). | 
| RabbitMQ          | `RabbitMQ` | |


### CONNECTION_STRING

The connection string format used is the same for all ServiceControl components.

- Azure Service Bus: <https://docs.particular.net/servicecontrol/transports#azure-service-bus>
- RabbitMQ: <https://docs.particular.net/servicecontrol/transports#rabbitmq>
- AmazonSQS: <https://docs.particular.net/servicecontrol/transports#amazon-sqs>

### RETURN_QUEUE

Default: `Particular.ServiceControl.Connector.MassTransit_return`

The return queue used by the connector that is passed to ServiceControl as the intermediate queue before returning the message back to the actual queue that MassTransit listens to.

### ERROR_QUEUE

Default: `error`

ServiceControl by default listens to the `error` queue but if this value is overriden in ServiceControl this configuration setting must be set to the same value.

### CUSTOM_CHECK_QUEUE

Default: `Particular.ServiceControl`

ServiceControl primary queue by default listens to `Particular.ServiceControl` queue but if this value is overriden in ServiceControl this configuration setting must be set to the same value.


### RABBITMQ_MANAGEMENT_API_URL

Default: None

Required when using RabbitMQ and error queues need to be dynamically resolved as queue information is queried on the broker to determine which error queues to listen to.

Example:

```txt
http://localhost:15672
```

### RABBITMQ_MANAGEMENT_API_USERNAME

Default: `guest`

The management api username.

### RABBITMQ_MANAGEMENT_API_PASSWORD

Default: `guest`

The management api username.


## Support

The MassTransit connector is currently in early access. If you have any queries or feedback, please let us know at <https://discuss.particular.net/>.

## Feedback

If you miss certain features or have any type of feedback then you can do this at:

- <https://github.com/Particular/ServiceControl.Connector.MassTransit>
- <https://discuss.particular.net/>

## Authors

This software, including this container image, is built and maintained by the team at Particular Software. See also the list of contributors who participated in this project.

## License

This project is licensed under the Reciprocal Public License 1.5 (RPL1.5) and commercial licenses are available - see the [source repository license file](https://github.com/Particular/ServiceControl/blob/master/LICENSE.md) for more information.
