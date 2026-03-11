namespace ServiceControl.Connector.MassTransit.AcceptanceTests.RabbitMQ;

using System;
using System.Security.Authentication;
using global::RabbitMQ.Client;

public static class ConnectionHelper
{
    static Lazy<ConnectionFactory> connectionFactory = new(() =>
    {
        var connectionConfiguration = AdapterRabbitMqConfiguration.ConnectionConfiguration.Create("host=localhost", "AcceptanceTests");

        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            HostName = connectionConfiguration.Host,
            Port = connectionConfiguration.Port,
            VirtualHost = connectionConfiguration.VirtualHost,
            UserName = connectionConfiguration.UserName ?? "guest",
            Password = connectionConfiguration.Password ?? "guest"
        };

        factory.Ssl.ServerName = factory.HostName;
        factory.Ssl.Certs = null;
        factory.Ssl.Version = SslProtocols.Tls12;
        factory.Ssl.Enabled = connectionConfiguration.UseTls;

        return factory;
    });

    public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
}