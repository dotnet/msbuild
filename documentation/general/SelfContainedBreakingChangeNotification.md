# [Breaking change]: Handling of command-line RuntimeIdentifier and SelfContained properties across project references

## Description

The `RuntimeIdentifier` and `SelfContained` properties can be specified on the command line to commands such as `dotnet build` and `dotnet publish`.
They can be specified either via parameters such as `-r` or `--self-contained`, or via the generic `-p:Key=Value` parameter, such as `-p:SelfContained=true`.

If these properties are specified on the command line, we've updated how they are applied (or not applied) to projects referenced by the initial project that is being built.

## Version

???

## Previous behavior

If `SelfContained` was specified on the command line, it would always flow to referenced projects.

`RuntimeIdentifier` would flow to referenced projects where either the `RuntimeIdentifier` or `RuntimeIdentifiers` properties were non-empty.

## New Behavior

Both `SelfContained` and `RuntimeIdentifier` will flow to a referenced project if any of the following are true for the referenced project:

- The `IsRidAgnostic` property is set to `false`
- The `OutputType` is `Exe` or `WinExe`
- Either the `RuntimeIdentifer` or `RuntimeIdentifiers` property is non-empty

## Type of breaking change

Source incompatible

## Reason for change

As of .NET SDK 6.0.100, we recommend specifying the value for self-contained on the command line if you specify the RuntimeIdentifier.
(This is because in the future we are considering [changing the logic](https://github.com/dotnet/designs/blob/main/accepted/2021/architecture-targeting.md)
so that specifying the RuntimeIdentifier on the command line doesn't automatically set the app to self-contained.)  We also added a warning message
to guide you to do so.

However, if you followed the warning and switched to a command specifying both the RuntimeIdentifier and the value for self-contained (for example
`dotnet build -r win-x64 --self-contained`), the command could fail if you referenced an Exe project, because the `RuntimeIdentifier` you specified
would not apply to the referenced project, but the `SelfContained` value would, and it's an error for an Exe project to have `SelfContained` set to
true without having a `RuntimeIdentifier` set.

## Recommended action

If you were relying on the `SelfContained` property to apply to all projects when it was specified on the command line, then you can get similar behavior
by setting `IsRidAgnostic` to false either in a file ([such as Directory.Build.props](https://docs.microsoft.com/visualstudio/msbuild/customize-your-build#directorybuildprops-and-directorybuildtargets)),
or as a command-line parameter such as `-p:IsRidAgnostic=false`.

## Open Questions

TODO: How does this apply to solutions?  Could a solution build set IsRidAgnostic to false for all projects, and would that fix other issues we have when specifying the RuntimeIdentifier for a solution build?
TODO: What happens if there's an Exe1 -> Library -> Exe2 reference, especially if there's also a direct reference from Exe1 -> Exe2
