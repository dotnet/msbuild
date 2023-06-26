# Workload Clean

## Introduction & Background 
`dotnet workload clean` is a command designed in .NET 8. 

Sometimes if uninstalls are interrupted or other unknown issues occur, workload packs can become orphaned on a developer machine.
Customers have reported many broken states -- some with their manifest files, others because of the potential orphaned pack(s).
These orphaned packs that the SDK did not correctly remove can reside on the file system directly or in the registry as garbage registry keys.

The point of this command is to help users:
- Clean up residue on their system, saving disk space.
- Help potentially restore their machine to a working state with workloads.
- Mass uninstall every workload in one swoop.

We considered `workload clean` as an option to "clean" or restore manifests to known SDK states; however, because of the existence of `workload repair`, we decided that is poor UX and can be done in that command.

## Command Spec

`dotnet workload clean`:

Runs workload [garbage collection](https://github.com/dotnet/designs/blob/main/accepted/2021/workloads/workload-installation.md#workload-pack-installation-records-and-garbage-collection) for either file-based or MSI-based workloads.
Under this mode, garbage collection behaves as normal, cleaning only the orphaned packs themselves and not their records. This means it will clean up orphaned packs from uninstalled versions of the .NET SDK or packs where installation records for the pack no longer exist.
This will only impact packs of feature bands below, or at the current feature band number of the SDK running `workload clean`, as the overall workload pack structural design may change in later versions.

Lists all Visual Studio Workloads installed on the machine and warns that they must be uninstalled via Visual Studio instead of the .NET SDK CLI.
This is to provide clarity as to why some workloads are not cleaned/uninstalled after running `dotnet workload clean`. 

`dotnet workload clean --all`:

Unlike workload clean, `workload clean --all` runs garbage collection irregularly, meaning that it cleans every existing pack on the machine that is not from VS, and is of the current SDK workload installation type. (Either File-Based or MSI-Based.)

Because of this, it also removes all workload installation records for the running .NET SDK feature band and below. `workload clean` does not yet remove installation records, as the manifests are currently the only way to map a pack to the workload ID, but the manifest files may not exist for orphaned packs. 
