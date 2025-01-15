# Particular Software ServiceControl MassTransit connector

An extension to [ServiceControl](https://docs.particular.net/servicecontrol) that adds support for processing [MassTransit](https://masstransit.io/) failures with the [Particular Platform](https://particular.net/service-platform). This extension makes all the recoverability feature for managing message processing failures; like (group) retrying, message editing and failed message inspection; available to the MassTransit community.  

## How to use this image

This extension currently supports both RabbitMQ and Azure Service Bus brokers.  
In order to successfully run this extension, you need to first run the ServiceControl container. Read more about the ServiceControl [image](https://hub.docker.com/r/particular/servicecontrol). 

### 1. Setup the connector

Before the connector can be run, the connector needs:

- some infrastructure queues created in the broker
- a text file with a list of error queues to monitor

To do this you need to run the `setup` command:
```shell
docker run -e TRANSPORT_TYPE=<RabbitMQ|AzureServiceBus|AzureServiceBusDeadLetter> -e CONNECTION_STRING=<connection string> --rm particular/servicecontrol-connector-masstransit:latest setup
```

The `setup` command will output onto the console a list of applicable queues that have been found in the broker.
By default, we only list the queues that end with `_error` (the default naming convention for MassTransit error queues), if you need to specify a different filter add `--filter <regular expression>`.

#### Example of a RabbitMQ setup

Assuming a RabbitMQ message broker is also hosted in a Docker container. Replace the <port>, <username> and <password> sections with their respective values.

```shell
docker run -e TRANSPORT_TYPE=RabbitMQ -e CONNECTION_STRING=host=host.docker.internal -e RABBITMQ_MANAGEMENT_API_URL=http://host.docker.internal:<port> -e RABBITMQ_MANAGEMENT_API_USERNAME=<username> -e RABBITMQ_MANAGEMENT_API_PASSWORD=<password> --rm particular/servicecontrol-connector-masstransit:latest setup
```

#### Example of an Azure Service Bus setup

```shell
docker run -e TRANSPORT_TYPE=AzureServiceBus -e CONNECTION_STRING=Endpoint=sb://[NAMESPACE].servicebus.windows.net/;SharedAccessKeyName=[KEYNAME];SharedAccessKey=[KEY] --rm particular/servicecontrol-connector-masstransit:latest setup
```

#### Example of an Azure Service Bus with Dead Letter enabled setup

Using the `--filter` option.

```shell
docker run -e TRANSPORT_TYPE=AzureServiceBusDeadLetter -e CONNECTION_STRING=Endpoint=sb://[NAMESPACE].servicebus.windows.net/;SharedAccessKeyName=[KEYNAME];SharedAccessKey=[KEY] --rm particular/servicecontrol-connector-masstransit:latest setup --filter "^production_.*"
```

### 2. Configure error queues to monitor

The connector won't start unless a list of error queues to monitor have been specified.  
From the previous step, you should have a list of error queues output to the console. If the console did not return any queues, it may be because MassTransit only creates the error queue for a consumer on demand, in this case you need to specify the list of queues manually.  

**It is important to review the list of queues and ensure that the connector is only monitoring the error queues that you want.**  

Save the list to a text file in your host OS, e.g. `queues.txt`.

### 3. Run the connector

The last step is to map the queues text file to the docker container `-v [local_path_to_queues_file]:/app/queues.txt:ro`, then run the connector to bridge MassTransit errors queues with the Particular Platform.

```shell
docker run -e TRANSPORT_TYPE=<RabbitMQ|AzureServiceBus|AzureServiceBusDeadLetter> -e CONNECTION_STRING=<connection string> -v [local_path_to_queues_file]:/app/queues.txt:ro particular/servicecontrol-connector-masstransit:latest run
```

#### Example of running with RabbitMQ

Assuming a RabbitMQ message broker is also hosted in a Docker container. Replace the <port>, <username> and <password> sections with their respective values.

```shell
docker run -e TRANSPORT_TYPE=RabbitMQ -e CONNECTION_STRING=host=host.docker.internal -e RABBITMQ_MANAGEMENT_API_URL=http://host.docker.internal:15672 -e RABBITMQ_MANAGEMENT_API_USERNAME=guest -e RABBITMQ_MANAGEMENT_API_PASSWORD=guest -v $(pwd)/queues.txt:/app/queues.txt:ro particular/servicecontrol-connector-masstransit:latest run
```

#### Example of running with Azure Service Bus

```shell
docker run -e TRANSPORT_TYPE=AzureServiceBus -e CONNECTION_STRING=Endpoint=sb://[NAMESPACE].servicebus.windows.net/;SharedAccessKeyName=[KEYNAME];SharedAccessKey=[KEY] -v $(pwd)/queues.txt:/app/queues.txt:ro --rm particular/servicecontrol-connector-masstransit:latest run
```

#### Example of running with Azure Service Bus with Dead Letter enabled

Using the `--filter` option.

```shell
docker run -e TRANSPORT_TYPE=AzureServiceBusDeadLetter -e CONNECTION_STRING=Endpoint=sb://[NAMESPACE].servicebus.windows.net/;SharedAccessKeyName=[KEYNAME];SharedAccessKey=[KEY] -v $(pwd)/queues.txt:/app/queues.txt:ro --rm particular/servicecontrol-connector-masstransit:latest run
```

## Refreshing the errors queue text file

The `queues-list` command can be run when you need to update the list of error queues.  
The text file containing queue names can be updated without bringing down the container.  

```shell
docker run -e TRANSPORT_TYPE=<RabbitMQ|AzureServiceBus|AzureServiceBusDeadLetter> -e CONNECTION_STRING=<connection string> --rm particular/servicecontrol-connector-masstransit:latest queues-list
```

This will output the list of all queues that end with `_error` (you can specify a different filter by using `--filter "a regular expression"`).

**It is important to review the list of queues and ensure that the connector is only monitoring the error queues that you want.**  

Copy that list to the queues text file that your running container is using.

## Installing the license

By default the connector can run in "trial" mode for up to 14 days, and after that you need to contact Particular to get a free license.
Once you have received a license from Particular, there are two options to install it to be used by the connector container.

- Using a volume map
   
  ```shell
  -v [local_path_to_license_xml_file]:/usr/share/ParticularSoftware/license.xml
  ```
- or using an environment variable

  ```shell
  -e PARTICULARSOFTWARE_LICENSE=[the full multi-line contents of the license file]
  ```

  For more information read [this guide](https://docs.particular.net/nservicebus/licensing/#license-management-environment-variable).

## Full list of environment variables

| Key                              | Description                                                                                          | Default                                                  |
|----------------------------------|------------------------------------------------------------------------------------------------------|----------------------------------------------------------|
| TRANSPORT_TYPE                   | The transport type.                                                                                  | None                                                     |
| CONNECTION_STRING                | The connection string for the specified transport.                                                   | None                                                     |
| RETURN_QUEUE                     | The intermediate queue used by the connector to which ServiceControl will send its retried messages. | `Particular.ServiceControl.Connector.MassTransit_return` |
| ERROR_QUEUE                      | The error queue ServiceControl ingests.                                                              | `error`                                                  |
| SERVICECONTROL_QUEUE             | The ServiceControl endpoint queue.                                                                   | `Particular.ServiceControl`                              |
| RABBITMQ_MANAGEMENT_API_URL      | RabbitMQ management API url when RabbitMQ is selected as transport.                                  | None                                                     |
| RABBITMQ_MANAGEMENT_API_USERNAME | RabbitMQ management API username.                                                                    | `guest`                                                  |
| RABBITMQ_MANAGEMENT_API_PASSWORD | RabbitMQ management API password.                                                                    | `guest`                                                  |
| PARTICULARSOFTWARE_LICENSE       | The Particular Software license.                                                                     |                                                          |

### TRANSPORT_TYPE

Currently support as the most used MassTransit transports: Amazon SQS, Azure Service Bus and RabbitMQ.

| Description       | Key                                  | Notes |
|-------------------|--------------------------------------| --- |
| Azure Service Bus | `AzureServiceBus`         | |
| Azure Service Bus with Dead Letter | `AzureServiceBusDeadLetter`         | Azure Service Bus configured to ingest from [dead-letter queues](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues). | 
| RabbitMQ          | `RabbitMQ` | |


### CONNECTION_STRING

The connection string format used is the same for all ServiceControl components.

- Azure Service Bus: <https://docs.particular.net/servicecontrol/transports#azure-service-bus>
- RabbitMQ: <https://docs.particular.net/servicecontrol/transports#rabbitmq>

### RETURN_QUEUE

Default: `Particular.ServiceControl.Connector.MassTransit_return`

The return queue used by the connector that is passed to ServiceControl as the intermediate queue before returning the message back to the actual queue that MassTransit listens to.

### ERROR_QUEUE

Default: `error`

ServiceControl by default listens to the `error` queue but if this value is overriden in ServiceControl this configuration setting must be set to the same value.

### SERVICECONTROL_QUEUE

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

### PARTICULARSOFTWARE_LICENSE

The Particular Software license, the environment variable should contain the full multi-line contents of the license file.  
A license file can also be volume-mounted to the container `-v license.xml:/usr/share/ParticularSoftware/license.xml`.

## Support

The MassTransit connector is currently in early access. If you have any queries or feedback, please let us know at <https://discuss.particular.net/>.

## Feedback

If you miss certain features or have any type of feedback then you can do this at:

- <https://github.com/Particular/ServiceControl.Connector.MassTransit>
- <https://discuss.particular.net/>

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

## Authors

This software, including this container image, is built and maintained by the team at Particular Software. See also the list of contributors who participated in this project.

## License

This project is licensed under the Reciprocal Public License 1.5 (RPL1.5) and commercial licenses are available - see the [source repository license file](https://github.com/Particular/ServiceControl/blob/master/LICENSE.md) for more information.
