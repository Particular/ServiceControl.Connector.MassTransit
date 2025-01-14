using System.Reflection;

public static class ConnectorVersion
{
    public static string Version { get; } = GetFileVersion();

    static string GetFileVersion()
    {
        var customAttributes = typeof(ConnectorVersion).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);

        if (customAttributes.Length >= 1)
        {
            var fileVersionAttribute = (AssemblyInformationalVersionAttribute)customAttributes[0];
            var informationalVersion = fileVersionAttribute.InformationalVersion;
            return informationalVersion.Split('+')[0];
        }

        return typeof(ConnectorVersion).Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";
    }
}