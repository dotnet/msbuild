# MSBuild release process

This is a description of the steps required to release MSBuild. It is **incomplete**; when something not present here is discovered to be required it should be added.

## Final branding/versioning

To produce packages without a `-prerelease` suffix, we need to specify `<DotNetFinalVersionKind>release</DotNetFinalVersionKind>` (see the [Arcade versioning docs]). This is ideally done on the same line as the version specification so that it causes a Git merge conflict when merging to the next release's branch. See [#6902](https://github.com/dotnet/msbuild/pull/6902) for an example.

[Arcade versioning docs]: https://github.com/dotnet/arcade/blob/31cecde14e1512ecf60d2d8afb71fd240919f4a8/Documentation/CorePackages/Versioning.md

## Public API

As of [#7018](https://github.com/dotnet/msbuild/pull/7018), MSBuild uses a Roslyn analyzer to ensure compatibility with assemblies compiled against older versions of MSBuild. The workflow of the analyzer is:

1. The analyzer keeps the `PublicAPI.Unshipped.txt` files updated.
2. New API surface goes into `PublicAPI.Unshipped.txt`.
3. At release time, we must manually promote the `Unshipped` public API to `Shipped`.

That is a new step in our release process for each formal release (including patch releases if they change API surface).

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
