# Trusted X.509 Certificate Roots

This directory contains [Microsoft-maintained root certificate trust lists (CTL)](https://learn.microsoft.com/security/trusted-root/program-requirements) for [Code Signing (CS)](https://en.wikipedia.org/wiki/Code_signing), and [Time Stamping (TS)](https://en.wikipedia.org/wiki/Code_signing#Time-stamping). The .NET SDK uses these CTLs for [NuGet signing and validation](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference), on Linux and macOS. They are also used in various Microsoft products. 

- [Code signing CTL](codesignctl.pem)
- [Timestamping CTL](timestampctl.pem)

The CTLs are stored in [PEM format](https://en.wikipedia.org/wiki/Privacy-Enhanced_Mail). 

## Behavior

[NuGet uses these CTLs as a fallback](https://github.com/dotnet/sdk/issues/25686) when an OS-provided CTL is not available.

- Linux: The fallback CTL is typically used; however, an OS-provided CTL will be used if available (see `ca-certificates` section).
- macOS: Package verification is not enabled by default, but when it is enabled, the fallback code signing CTL is always used since an OS-provided CTL is not available.
- Windows: The fallback CTL is never used since an OS-provided CTL is always available (via an OS API).

## Linux

On Linux, NuGet will first probe for a code signing system bundle (multi-PEM file) using a [list of well-known paths](https://github.com/dotnet/designs/blob/main/accepted/2021/signed-package-verification/re-enable-signed-package-verification-technical.md#linux). The first successful match will be used. If no match is found or if there are problems processing the system bundle, NuGet will use the fallback bundle.

The timestamping CTL in the .NET SDK is always used. There doesn't seem to be any precedent for a timestamping-specific certificate bundle under `/etc/pki/ca-trust/extracted/pem`.

The `ca-certificates` package contains trusted roots on most Linux distributions. Some distributions hold the view that this package should be the sole source of roots. That approach results in a single package affecting the overall trust model (as it relates to X.509 certificates) of the machine/container. We are able to accommodate that approach for code signing root certificates.    

Distributions are welcome to source the code signing roots for their `ca-certificates` package and to install them [according to our spec](https://github.com/dotnet/designs/blob/main/accepted/2021/signed-package-verification/re-enable-signed-package-verification-technical.md#linux). In that case, the fallback code signing CTL will not be used.

## macOS

On macOS, NuGet signed package verification is not enabled by default, due to the following issues:

- https://github.com/NuGet/Home/issues/11985
- https://github.com/NuGet/Home/issues/11986

## Governance

Roots included in the respective CTLs conform to program requirements outlined by the [Microsoft Trusted Root Program](https://docs.microsoft.com/security/trusted-root/program-requirements). 

Microsoft will typically update the CTLs in this repository within 30 days after [updates are published for Microsoft products](https://docs.microsoft.com/security/trusted-root/release-notes).

The CTLs are provided on an as-is basis, at no cost, and under the MIT license (same as this repo).

Issues can be filed at [dotnet/sdk](https://github.com/dotnet/sdk/issues) or [NuGet/Home](https://github.com/NuGet/Home/issues).
