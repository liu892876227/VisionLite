﻿## Description

The package **'Beckhoff.TwinCAT.Ads.ConfigurationProviders'** contains ConfigurationProviders that can be used in the context of the
Beckhoff.TwinCAT.Ads.TcpIpRouter and Beckhoff.TwinCAT.Ads.AdsOverMqtt packages context.
It implements TwinCAT specific configuration providers and Ads logging providers that can be used out of the box within your Hosting application.

For example TwinCAT.Ads.Configuration.StaticRoutesXmlConfigurationProvider implements to find and read the TwinCAT StaticRoutes.xml file for usage
as configuration provider.
The class TwinCAT.Ads.Logging.AdsLoggerProvider offers a logging provider for ADS/Ams logging.

[Microsoft Learn: Configuration Providers](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
[Microsoft Learn: Logging Providers](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-providers)
## Requirements

- **.NET 8.0**, **.NET 6.0** or **.NET Standard 2.0** (e.g. >= **.NET Framework 4.61**) compatible SDK

## Installation
Place a dependency of this package in your application.

## Version Support lifecycle

| Package | Description | .NET Framework | TwinCAT | Active Support |
|---------|-------------|----------------|---------|-----------------
6.2 | Package basing on .NET 8.0 | net8.0 | >= 3.1.4024.10 [^1] | X |
[^1]: Requirement on the Host system. No version limitation in remote system communication.
[^2]: Microsoft support for .NET5 ends with May 8, 2022. Therefore it is recommended to update **Beckhoff.TwinCAT** packages from Version 5 to Version 6.

[Migrating to the latest .NET](https://docs.microsoft.com/en-us/dotnet/architecture/modernize-desktop/example-migration)
[Microsoft .NET support lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

## First Steps

```csharp
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using TwinCAT.Ads.ConfigurationProviders;

    class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public async static Task Main(string[] args)
        {
            var ret = CreateHostBuilder(args);
            await ret.RunConsoleAsync();
        }

        /// <summary>
        /// Creates the host builder.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>IHostBuilder.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var ret = Host.CreateDefaultBuilder(args);

            ret.ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<AdsOverMqttService>();
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                // Add further AppConfigurationProvider here.
                //config.Sources.Clear(); // Clear all default config sources 
                //config.AddEnvironmentVariables("ADSOVERMQTT_"); // Use Environment variables
                //config.AddCommandLine(args); // Use Command Line
                //config.AddJsonFile("appSettings.json"); // Use Appsettings
                config.AddStaticRoutesXmlConfiguration(); // Overriding settings with StaticRoutes.Xml 
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                // Adding console logging here.
                logging.AddConsole();
            });
            return ret;
        }
    }
```

## Further documentation

The actual version of the documentation is available in the Beckhoff Infosys:
[Beckhoff Information System](https://infosys.beckhoff.com/index.php?content=../content/1033/tc3_ads.net/index.html&id=207622008965200265)

## Sample Code

Demo Code for the usage of the contained providers:
[Beckhoff GitHub Mqtt Sample](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/ClientSamples/AdsOverMqttApp)
[Beckhoff GitHub Docker Samples](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/DockerSamples)
