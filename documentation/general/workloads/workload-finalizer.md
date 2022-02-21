# .NET SDK Installer Finalizer

When the .NET SDK is uninstalled, any workloads that were installed with that SDK should also be uninstalled.  In order to do this when uninstalling an SDK which was installed via the standalone EXE installer bundle, there is a piece of native code called the finalizer.  The finalizer removes any reference counts on workload packs and manifests from the SDK that is being uninstalled, and uninstalls any MSIs which then have no reference counts.

The finalizer code is here: https://github.com/dotnet/installer/tree/main/src/finalizer