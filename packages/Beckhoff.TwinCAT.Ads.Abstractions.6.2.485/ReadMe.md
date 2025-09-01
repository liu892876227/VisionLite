## Description

The package **'Beckhoff.TwinCAT.Ads.Abstractions'** contains interfaces and base implementations for the **'Beckhoff.TwinCAT.Ads.Server'** and
**'Beckhoff.TwinCAT.Ads'** packages. It is never used standalone and is a dependency of the above-named packages.

## Requirements

- **.NET 8.0**, **.NET 6.0** or **.NET Standard 2.0** (e.g. >= **.NET Framework 4.61**) compatible SDK
- A **TwinCAT 3.1.4024.10** Build (XAE, XAR or ADS Setup) or later.

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

## Installation

As dependency of other Beckhoff packages

## Further documentation

The actual version of the documentation is available in the Beckhoff Infosys.
[Beckhoff Information System](https://infosys.beckhoff.com/index.php?content=../content/1033/tc3_ads.net/index.html&id=207622008965200265)