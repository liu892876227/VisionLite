## Description

The package 'Beckhoff.TwinCAT.Ads' contains a client implementation for the ADS Communication protocol used by .NET Core and .NET Full Framework.
It includes everything to develop own .NET applications (e.g. HMI, Datalogger) to communicate with TwinCAT devices (e.g. PLC, NC or IO-devices).

The Root object is the **TwinCAT.Ads.AdsClient** to communicate to all variants of local and remote ADS servers and devices or the AdsSession object.

## Requirements

- **.NET 8.0**, **.NET 6.0** or **.NET Standard 2.0** (e.g. >= **.NET Framework 4.61**) compatible SDK
- A **TwinCAT 3.1.4024.10** Build (XAE, XAR or ADS Setup) or later.

## Installation

### TwinCAT Version >= 4024.10

Because the Beckhoff.TwinCAT.Ads Version 6.X uses internal interfaces that are available only from TwinCAT 4024.10 on, an appropriate
version must be installed locally. The package doesn't work with older installations. An alternativ approach for some use cases is
to use the 'Beckhoff.TwinCAT.Ads.AdsRouterConsole' / 'Beckhoff.TwinCAT.TcpIpRouter' packages to establish your own router.

### Systems without TwinCAT Installation

There are options to run AdsClient/AdsServer instances without having a full TwinCAT system installed on the host system.

- Use of the TwinCAT UM Runtime on supported systems
- Usage of AdsOverMqtt via a Mqtt Broker (Beckhoff.TwinCAT.Ads >= 6.2 necessary)
- Running a slim (feature reduced) .NET implemented ADS Router (formerly AdsRouterConsole). The implementation is in the 'Beckhoff.TwinCAT.Ads.TcpRouter' package

For more information please have a look at:
[Beckhoff GitHub RouterSamples](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/RouterSamples)
[Beckhoff GitHub DockerSamples](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/DockerSamples)

### Version Support lifecycle

| Package | Description | .NET Framework | TwinCAT | Active Support |
|---------|-------------|----------------|---------|-----------------
6.2 | Package basing on .NET 8.0/6.0 | net8.0, net6.0, netstandard2.0 | >= 3.1.4024.10 [^1] | X |
6.1 | Package basing on .NET 7.0 [^2]/6.0 | net7.0, net6.0, netstandard2.0 | >= 3.1.4024.10 [^1] | X |
6.0 | Package basing on .NET 6.0 | net6.0, netcoreapp3.1, netstandard2.0, net461 | >= 3.1.4024.10 [^1] |  |
4.x | Package basing on .NET Framework 4.0 | net4 | All | X |

[^1]: Requirement on the Host system. No version limitation in remote system communication.
[^2]: Microsoft support for .NET7 ends with May 14, 2024. Therefore it is recommended to update .NET Applications from Version 7 to Version 8.

[Migrating to the latest .NET](https://docs.microsoft.com/en-us/dotnet/architecture/modernize-desktop/example-migration)
[Microsoft .NET support lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

### First Steps

The following code instantiates an AdsClient object, connects to a target device (here the local System Service)
and reads the ADS state asynchronously.

```csharp
using System;
using System.Threading.Tasks;
using System.Threading;
using TwinCAT.Ads;

namespace AdsAsyncTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AdsClient myClient = new AdsClient();
            try
            {
                // Connect to local TwinCAT System Service
                myClient.Connect(AmsNetId.Local, 10000);
                ResultReadDeviceState result = await myClient.ReadStateAsync(CancellationToken.None);
                Console.WriteLine("State: " + result.State.AdsState);
                Console.WriteLine("Press key to exit...");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message.ToString());
            }
            finally
            {
                myClient.Dispose();
            }
        }
    }
}
```

Please be aware, that the AdsRouteConsole doesn't provide the AmsPort 10000, due to the missing TwinCAT System Service. Therefore the AmsNetId of the connection
must be changed to a registered remote system.

## Further Documention

The actual version of the documentation is available in the Beckhoff Infosys.
[Beckhoff Information System](https://infosys.beckhoff.com/index.php?content=../content/1033/tc3_ads.net/index.html&id=207622008965200265)

There are a few breaking changes in the new version to enable asynchronous programming, reducing the memory footprint and enhancement of the performance.

- Renaming the TcAdsClient class to AdsClient
- Changing synchronous code to 'async'. The new asynchronous methods are indicated with “MethodName**Async**” and could be used very similar to their synchronous counterparts.
- For using .NET Core more efficiently, all the **AdsStream** class appearances in method interfaces are replaced by the new more efficient **Span<byte>** and **Memory[byte]** classes.
- **AdsBinaryReader** and **AdsBinaryWriter** should be replaced by using the standard BinaryReader and/or **System.Buffers.Binary.BinaryPrimitives** Methods.

More details can be read in the documentation under:
[HowTo Samples](https://infosys.beckhoff.com/content/1033/tc3_ads.net/9407530763.html?id=1865588818185263387) --> [Upgrading existing ADS Application code (Version 4.X --> 5.X)](https://infosys.beckhoff.com/content/1033/tc3_ads.net/9407536907.html?id=2410235194236726912)

## Sample Code

[Beckhoff GitHub BaseSamples](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/BaseSamples)
[Beckhoff GitHub ClientSamples](https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/tree/main/Sources/ClientSamples)
