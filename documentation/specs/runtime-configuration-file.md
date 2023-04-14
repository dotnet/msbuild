# Runtime Configuration Files

The runtime configuration files store the dependencies of an application (formerly stored in the `.deps` file). They also include runtime configuration options, such as the Garbage Collector mode. Optionally they can also include data for runtime compilation (compilation settings used to compile the original application, and reference assemblies used by the application).

## What produces the files and where are they?

There are two runtime configuration files for a particular application. Given a project named `MyApp`, the compilation process produces the following files (on Windows, other platforms are similar):

* `MyApp.dll` - The managed assembly for `MyApp`, including an ECMA-compliant entry point token.
* `MyApp.exe` - A copy of the `apphost.exe` executable. This is present when the application is self-contained, and in newer functionality (2.1.0+) for framework-dependent applications that wish to support platform-specific (non-portable) executables.
* `MyApp.runtimeconfig.json` - An **optional** configuration file containing runtime configuration settings. This file is required for framework-dependent applications, but not for self-contained apps.
* `MyApp.runtimeconfig.dev.json` - An **optional** configuration file containing runtime configuration settings that typically only exists in a non-published output and thus is used for development-time scenarios. This file typically specifies additional probing paths. Depending on the semantics of each setting, the setting is either combined with or overridden by the values from `MyApp.runtimeconfig.json`.
* `MyApp.deps.json` - A list of dependencies, compilation dependencies and version information used to address assembly conflicts. Not technically required, but required to use the servicing or package cache/shared package install features, and to assist during roll-forward scenarios to select the newest version of any assembly that exists more than once in the application and framework(s). If the file is not present, all assemblies in the current folder are used instead.

The `MyApp.runtimeconfig.json` is designed to be user-editable (in the case of an app consumer wanting to change various CLR runtime options for an app, much like the `MyApp.exe.config` XML file works in .NET 4.x today). However, the `MyApp.deps.json` file is designed to be processed by automated tools and should not be user-edited. Having the files as separate makes this clearer. We could use a different format for the deps file, but if we're already integrating a JSON parser into the host, it seems most appropriate to re-use that here. Also, there are diagnostic benefits to being able to read the `.deps.json` file in a simple text editor.

**IMPORTANT**: Framework-dependent applications have some adjustments to this spec which are covered at the end.

## File format

The files are both JSON files stored in UTF-8 encoding. Below are sample files. Note that not all sections are required and some will be opt-in only (see below for more details). The `.runtimeconfig.json` file is completely optional, and in the `.deps.json` file, only the `runtimeTarget`, `targets` and `libraries` sections are required (and within the `targets` section, only the runtime-specific target is required). If no `.deps.json` exists, all assemblies local to the app will be added as TPA (trusted platform assemblies).

### [appname].runtimeconfig.json
```json
{
    "runtimeOptions": {
        "configProperties": {
            "System.GC.Server": true,
            "System.GC.Concurrent": true,
            "System.Threading.ThreadPool.MinThreads": 4,
            "System.Threading.ThreadPool.MaxThreads": 8
        },
        "tfm": "netcoreapp2.1",
        "framework": {
            "name": "Microsoft.NETCore.App",
            "version": "2.1.0"
        },
        "applyPatches": true,
        "rollForwardOnNoCandidateFx": 1
    }
}
```

### [appname].deps.json
```json
{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v2.0",
    "signature": "aafc507050a6c13a0cf2d6d4c3de136e6571da6e"
  },
  "compilationOptions": {
    "defines": [
		"TRACE",
		"DEBUG"
    ],
    "languageVersion": "",
    "platform": "",
    "warningsAsErrors": false,
  },
  "targets": {
    ".NETCoreApp,Version=v2.1": {
      "MyApp/1.0.0": {
        "dependencies": {
          "System.Banana": "1.0.0"
        },
        "runtime": {
          "MyApp.dll": {}
        },
        "compile": {
          "MyApp.dll": {}
        }
      },
      "System.Banana/1.0.0": {
        "dependencies": {
          "System.Foo": "1.0.0"
        },
        "runtime": {
          "lib/netcoreapp2.1/System.Banana.dll": {
            "assemblyVersion": "1.0.0.0",
            "fileVersion": "1.0.0.0"
          }
        },
        "compile": {
          "ref/netcoreapp2.1/System.Banana.dll": {}
        }
      },
      "System.Foo/1.0.0": {
        "runtime": {
          "lib/netcoreapp2.1/System.Foo.dll": {
            "assemblyVersion": "1.0.0.0",
            "fileVersion": "1.0.0.0"
          }
        },
        "compile": {
          "ref/netcodeapp2.1/System.Foo.dll": {}
        }
      }
    }
  },
  "libraries": {
    "MyApp/1.0.0": {
      "type": "project",
      "serviceable": false,
      "sha512": ""
    },
    "System.Banana/1.0.0": {
      "type": "package",
      "serviceable": true,
      "sha512": "sha512-C63ok7q+Fi6O6I/WB4ut3hFibGSraUlE461LxhhwNk5Vcdl4ijDhX1QDupDdp3Cxr7TgwB55Sd4zNtlwT7ksAA==",
      "path": "system.banana/1.0.0",
      "hashPath": "system.banana.1.0.0.nupkg.sha512"
    },
    "System.Foo/1.0.0": {
      "type": "package",
      "serviceable": true,
      "sha512": "sha512-avYGOiBQ4U/fJfzEDF7lzPLhk/w6P9/28y0iiQh3AxlWOheuZTgXA/pzuORuAu/s5B2bXHO2BlvQKZN0PfQ2HQ==",
      "path": "system.foo/1.0.0",
      "hashPath": "system.foo.1.0.0.nupkg.sha512"
    }
  }
}
```

## Sections

### `runtimeOptions` Section (`.runtimeconfig.json`)

This section is created when building a project. Settings include:
* `configProperties` - Indicates configuration properties to configure the runtime and the framework
  * Examples:
    * Full list of [configuration properties](https://github.com/dotnet/docs/blob/main/docs/core/runtime-config/index.md) for CoreCLR.
    * `System.GC.Server` (old: `gcServer`) - Boolean indicating if the server GC should be used (Default: `true`).
    * `System.GC.Concurrent` (old: `gcConcurrent`) - Boolean indicating if background garbage collection should be used.
* `framework` - Indicates the `name`, `version`, and other properties of the shared framework to use when activating the application including `applyPatches` and `rollForwardOnNoCandidateFx`. The presence of this section (or another framework in the new `frameworks` section) indicates that the application is a framework-dependent app.
* `rollForward` - Introduced in .NET Core 3.0. Determines roll-forward behavior. Values: `LatestPatch`, `Minor`, `Major`, `LatestMinor`, `LatestMajor`, `Disable`. See [high-level design](https://github.com/dotnet/designs/blob/main/accepted/2019/runtime-binding.md#rollforward) and [detailed design](https://github.com/dotnet/runtime/blob/main/docs/design/features/framework-version-resolution.md) for more information.
* `applyPatches` - **Deprecated in favor of `rollForward`**, please use `rollForward` property instead. When `false`, the most compatible framework version previously found is used. When `applyPatches` is unspecified or `true`, the framework from either the same or a higher version that differs only by the patch field will be used. See [roll-forward-on-no-candidate documentation](https://github.com/dotnet/runtime/blob/main/docs/design/features/roll-forward-on-no-candidate-fx.md) for more information.
* `rollForwardOnNoCandidateFx` - **Deprecated in favor of `rollForward`**, please use `rollForward` property instead - Determines roll-forward behavior. Only applies to `production` releases. Values: 0(Off), 1 (roll forward on [minor] or [patch]), 2 (Roll forward on [major], [minor] or [patch])
 See [roll-forward-on-no-candidate documentation](https://github.com/dotnet/runtime/blob/main/docs/design/features/roll-forward-on-no-candidate-fx.md) for more information.
* `frameworks` - This is an optional array added in 3.0 that allows multiple frameworks to be specified. The `name`, `version`, `rollForward` (.NET Core 3.0 +), `applyPatches` (deprecated) and `rollForwardOnNoCandidateFx` (deprecated) properties are available. The `framework` section is no longer necessary in 3.0, but if present is treated as if it was the first framework in the `frameworks` section. The presence of frameworks in this section (or the `framework` section) indicates that the application is a framework-dependent app. See the notes at the end of this document for more information.
* `additionalProbingPaths` - Optional property which specifies additional paths to consider when looking for dependencies. The value is either a single string, or an array of strings.
* `tfm` - Optional string value which specifies the Target Framework Moniker.

These settings are read by host (apphost or dotnet executable) to determine how to initialize the runtime. All versions of the host **must ignore** settings in this section that they do not understand (thus allowing new settings to be added in later versions).

### `compilationOptions` Section (`.deps.json`)

This section is created during build from the project's settings. The exact settings found here are specific to the compiler that produced the original application binary. Some example settings include: `defines`, `languageVersion` (e.g. the version of C# or VB), `allowUnsafe` (a C# option), etc.
This section is ignored by the runtime host. It is only potentially used by the application itself.

### `runtimeTarget` Section (`.deps.json`)

This property contains the name of the target from `targets` that should be used by the runtime. This is present to simplify the host so that it does not have to parse or understand target names and the meaning thereof.

### `targets` Section (`.deps.json`)

Each property under `targets` describes a "target", which is a collection of libraries required by the application when run or compiled in a certain framework and platform context. A target **must** specify a Framework name, and **may** specify a Runtime Identifier. Targets without Runtime Identifiers represent the dependencies and assets which are platform agnostic. These targets can also represent dependencies and assets which are used for compiling the application for a particular framework. Targets with Runtime Identifiers represent the dependencies and assets used for running the application under a particular framework and on the platform defined by the Runtime Identifier.
In the example above, the `.NETCoreApp,Version=v2.1` target lists the dependencies and assets used to run and compile the application for `netcoreapp2.1`.
If the app was published specifically for 64-bit Mac OS X 10.10 machine, it would also contain a target `.NETCoreApp,Version=v2.1/osx.10.10-x64` which would list the dependencies and assets used to run the application on that specific platform.

There will always at least one target in the `[appname].deps.json` file: the platform neutral list of runtime and compilation dependencies. In cases of a platform specific application there would be two targets: a compilation target, and a runtime target. The compilation target will be named with the framework name used for the compilation (`.NETCoreApp,Version=v2.1` in the example above). The runtime target will be named with the framework name and runtime identifier used to execute the application (`.NETCoreApp,Version=v2.5/osx.10.10-x64` in the example above). However, the runtime target will also be identified by name in the `runtimeOptions` section, so that the host does not need to parse and understand target names.

The content of each target property in the JSON is a JSON object. Each property of that JSON object represents a single dependency required by the application when compiled for/run on that target. The name of the property contains the ID and Version of the dependency in the form `[Id]/[Version]`. The content of the property is another JSON object containing metadata about the dependency.

The `dependencies` property of a dependency object defines the ID and Version of direct dependencies of this node. It is a JSON object where the property names are the ID of the dependency and the content of each property is the Version of the dependency.

The `runtime` property of a dependency object lists the relative paths to Managed Assemblies required to be available at runtime in order to satisfy this dependency. The paths are relative to the location of the Dependency (see below for further details on locating a dependency).

The `resources` property of a dependency object lists the relative paths and locales of Managed Satellite Assemblies which provide resources for other languages. Each item contains a `locale` property specifying the [IETF Language Tag](https://en.wikipedia.org/wiki/IETF_language_tag) for the satellite assembly (or more specifically, a value usable in the Culture field for a CLR Assembly Name).

The `native` property of a dependency object lists the relative paths to Native Libraries required to be available at runtime in order to satisfy this dependency. The paths are relative to the location of the Dependency (see below for further details on locating a dependency).

The `runtimeTargets` property of a dependency object lists RID-specific assets. This is only used for framework dependent applications. See the description of framework dependent apps below for more details.

The `compile` property of a dependency object lists the relative paths to Reference Assemblies which were used to compile the application.

If a given dependency is only listed for compilation, then its `runtime`, `resources` and `native` properties is omitted. Similarly if the dependency is only listed for runtime, then its `compile` property is omitted.

Only dependencies with a `type` value of `package` (as per the `libraries` section described below) should be considered by the host. There may be other items, used for other purposes (for example, Projects, Reference Assemblies, etc). Note that currently host basically ignores the `type` property.

### `libraries` Section (`.deps.json`)

This section contains a union of all the dependencies found in the various targets, and contains common metadata for them. Specifically, it contains:
* `type` - the type of the library. `package` for NuGet packages. `project` for a project reference. Can be other things as well.
* `path` - in the `package` library this is a relative path where to find the assets.
* `serviceable` - a boolean indicating if the library can be serviced (only for `package`-typed libraries)
* `sha512` - SHA-512 hash of the package file (`package`-typed libraries).
* `hashPath` - in the `package` library this is a relative path to the `.nupkg.sha512` hash file.

### `runtimes` Section (`.deps.json`)

This section is only present in the root framework's (so `Microsoft.NETCore.App`) `.deps.json` and it contains the RID fallback graph. See below for detailed description of this section. Example (trimmed):
```json
{
    "runtimes": {
        "win-x64": [
            "win",
            "any",
            "base"
        ]
    }
}
```


## How the file is used

The file is read by two different components:

* The host uses it to determine what to place on the TPA and Native Library Search Path lists, as well as what runtime settings to apply (GC type, etc.).
* `Microsoft.Extensions.DependencyModel` uses it to allow a running managed application to query various data about it's dependencies. For example:
  * To find all dependencies that depend on a particular package (used by ASP.NET MVC and other plugin-based systems to identify assemblies that should be searched for possible plugin implementations)
  * To determine the reference assemblies used by the application when it was compiled in order to allow runtime compilation to use the same reference assemblies (used by ASP.NET Razor to compile views)
  * To determine the compilation settings used by the application in order to allow runtime compilation to use the same settings (also used by ASP.NET Razor views).

## Opt-In Compilation Data

Some of the sections in the `.deps.json` file contain data used for runtime compilation. This data is not provided in the file by default. Instead, an MSBuild property `PreserveCompilationContext` must be set to `true` (typically in the project file) in order to ensure this data is added. Without this setting, the `compilationOptions` will not be present in the file, and the `targets` section will contain only the runtime dependencies.
Note that ASP.NET projects (those using `Microsoft.NET.Sdk.Web` SDK) has this property set to `true` by default.

## Framework-dependent Deployment Model

An application can be deployed in a "framework-dependent" deployment model. In this case, the RID-specific assets of packages are published within a folder structure that preserves the RID metadata. However the host does not use this folder structure, rather it reads data from the `.deps.json` file.

In the framework-dependent deployment model, the `*.runtimeConfig.json` file will contain the `runtimeOptions.framework` section:

```json
{
    "runtimeOptions": {
        "framework": {
            "name": "Microsoft.NETCore.App",
            "version": "1.0.1"
        }
    }
}
```

This data is used to locate the shared framework folder. The exact mechanics of which version are selected are defined in the [shared framework lookup](https://github.com/dotnet/core-setup/blob/main/Documentation/design-docs/multilevel-sharedfx-lookup.md) document. In general, it locates the shared runtime in the `shared` folder located beside it by using the relative path `shared/[runtimeOptions.framework.name]/[runtimeOptions.framework.version]`. Once it has applied any version roll-forward logic and come to a final path to the shared framework, it locates the `[runtimeOptions.framework.name].deps.json` file within that folder and loads it **first**.

Note, starting with 3.0, the "framework" section is optional and a new "frameworks" section supports multiple frameworks:
```json
{
    "runtimeOptions": {
        "frameworks": [
            {
                "name": "Microsoft.AspNetCore.All",
                "version": "3.0.0"
            },
            {
                "name": "Microsoft.Forms",
                "version": "3.0.0"
            }
        ]
    }
}
```

Next, the deps file from the application is loaded and (conceptually) merged into this deps file. Data from the app-local deps file trumps data from the shared framework.

The shared framework's deps file will also contain a `runtimes` section defining the fallback logic for all RIDs known to that shared framework. For example, a shared framework deps file installed into a Ubuntu machine may look something like the following:

```json
{
    "runtimeTarget": {
        "name": ".NETStandardApp,Version=v1.5",
        "portable": false
    },
    "targets": {
        ".NETStandardApp,Version=v1.5": {
            "System.Runtime/4.0.0": {
                "runtime": "lib/netstandard1.5/System.Runtime.dll"
            },
            "... other libraries ...": {}
        }
    },
    "libraries": {
        "System.Runtime/4.0.0": {
            "type": "package",
            "serviceable": true,
            "sha512": "[base64 string]"
        },
        "... other libraries ...": {}
    },
    "runtimes": {
        "ubuntu.15.04-x64": [ "ubuntu.14.10-x64", "ubuntu.14.04-x64", "debian.8-x64", "linux-x64", "linux", "unix", "any", "base" ],
        "ubuntu.14.10-x64": [ "ubuntu.14.04-x64", "debian.8-x64", "linux-x64", "linux", "unix", "any", "base" ],
        "ubuntu.14.04-x64": [ "debian.8-x64", "linux-x64", "linux", "unix", "any", "base" ]
    }
}
```

The host will detect the RID at runtime (for example, `ubuntu.14.04-x64` for Ubuntu 14.04 64bit). It will look up the corresponding entry in the `runtimes` section to identify what the fallback list is for `ubuntu.14.04-x64`. The fallbacks are identified from most-specific to least-specific. In the case of `ubuntu.14.04-x64` and the example above, the fallback list is: `"debian.8-x64", "linux-x64", "linux", "unix", "any", "base"` (note that an exact match on the RID itself is the first preference, followed by the first item in the fallback list, then the next item, and so on).

In the app-local deps file for a `framework-dependent` application, the package entries may have an additional `runtimeTargets` section detailing RID-specific assets. The host should use this data, along with the current RID and the RID fallback data defined in the `runtimes` section of the shared framework deps file to select one **and only one** RID value out of each package individually. The most specific RID present within the package should always be selected.

Consider an application built for `ubuntu.14.04-x64` and the following snippet from an app-local deps file (some sections removed for brevity).

```json
{
    "targets": {
        ".NETStandardApp,Version=v1.5": {
            "System.Data.SqlClient/4.0.0": {
                "compile": {
                    "ref/netstandard1.5/System.Data.SqlClient.dll": {}
                },
                "runtimeTargets": {
                    "runtimes/unix/lib/netstandard1.5/System.Data.SqlClient.dll": {
                        "assetType": "runtime",
                        "rid": "unix",
                        "assemblyVersion": "4.0.0.0",
                        "fileVersion": "4.5.12345.0"
                    },
                    "runtimes/win7-x64/lib/netstandard1.5/System.Data.SqlClient.dll": {
                        "assetType": "runtime",
                        "rid": "win7-x64",
                        "assemblyVersion": "4.0.0.0",
                        "fileVersion": "4.5.12345.0"
                    },
                    "runtimes/win7-x86/lib/netstandard1.5/System.Data.SqlClient.dll": {
                        "assetType": "runtime",
                        "rid": "win7-x86",
                        "assemblyVersion": "4.0.0.0",
                        "fileVersion": "4.5.12345.0"
                    },
                    "runtimes/win7-x64/native/sni.dll": {
                        "assetType": "native",
                        "rid": "win7-x64"
                    },
                    "runtimes/win7-x86/native/sni.dll": {
                        "assetType": "native",
                        "rid": "win7-x86"
                    }
                }
            }
        }
    }
}
```

When setting up the TPA and native library lists, it will do the following for the `System.Data.SqlClient` entry in the example above:

1. Add all entries from the root `runtime` and `native` sections (not present in the example). (Note: This is essentially the current behavior for the existing deps file format)
2. Add all appropriate entries from the `runtimeTargets` section, based on the `rid` property of each item:
  1. Attempt to locate any item for the RID `ubuntu.14.04-x64`. If any asset is matched, take **only** the items matching that RID exactly and add them to the appropriate lists based on the `assetType` value (`runtime` for managed code, `native` for native code)
  2. Reattempt the previous step using the first RID in the list provided by the list in the `runtimes."ubuntu.14.04-x64"` section of the shared framework deps file. If any asset is matched, take **only** the items matching that RID exactly and add them to the appropriate lists
  3. Continue to reattempt the previous search for each RID in the list, from left to right until a match is found or the list is exhausted. Exhausting the list without finding an asset, when a `runtimeTargets` section is present is **not** an error, it simply indicates that there is no need for a runtime-specific asset for that package.

Note one important aspect about asset resolution: The resolution scope is **per-package**, **not per-application**, **nor per-asset**. For each individual package, the most appropriate RID is selected, and **all** assets taken from that package must match the selected RID exactly. For example, if a package provides both a `linux-x64` and a `unix` RID (in the `ubuntu.14.04-x64` example above), **only** the `linux-x64` asset would be selected for that package. However, if a different package provides only a `unix` RID, then the asset from the `unix` RID would be selected.

The path to a runtime-specific asset is resolved in the same way as a normal asset (first check Servicing, then Package Cache, App-Local, Global Packages Location, etc.) with **one exception**. When searching app-local, rather than just looking for the simple file name in the app-local directory, a runtime-specific asset is expected to be located in a subdirectory matching the relative path information for that asset in the lock file. So the `native` `sni.dll` asset for `win7-x64` in the `System.Data.SqlClient` example above would be located at `APPROOT/runtimes/win7-x64/native/sni.dll`, rather than the normal app-local path of `APPROOT/sni.dll`.

Each entry in the `runtime` or `runtimeTargets` sections can also have `assemblyVersion` and `fileVersion` properties. These specify the assembly and file version of the assembly being referenced. These versions are used when resolving assemblies based on roll-forward settings. See the [Multi Level Shared FX Lookup](https://github.com/dotnet/core-setup/blob/main/Documentation/design-docs/multilevel-sharedfx-lookup.md#hostpolicy-changes-for-21) for more details.

## Additional information on runtimeconfig.json framework settings (3.0+)
With the addition of the `frameworks` section in 3.0, an application (or another framework) can reference multiple frameworks. This is necessary when more than one framework is being used by the application (or framework). Previously, an application or framework could only reference one framework, causing a "chain" of frameworks. Now, with multiple frameworks at each level, a "graph" or "tree" of frameworks is supported.

In addition to specifying a dependency on more than one framework, the `frameworks` section can also be used to override settings from a framework's `runtimeconfig.json`; this should only be done with the understanding of all consequences including preventing roll-forward compatibility to future versions. The settings include `version`, `rollForwardOnNoCandidateFx` and `applyPatches`, with `version` the most likely value to be changed.

Overriding a value is always "most restrictive". This means if `applyPatches` is already `false` in a lower-level framework, then it cannot be changed to `true`. For `rollForwardOnNoCandidateFx` the value 0=off is the most restrictive, then 1=minor\patch, then 2=major\minor\patch. For `version`, the highest version requested will be used.

As an example of overriding settings, assume the following framework layering:
- Application
- Microsoft.AspNetCore.All
- Microsoft.AspNetCore.App
- Microsoft.NetCore.App

Except for Microsoft.NetCore.App (since it does not have a lower-level framework), each layer has a runtimeconfig.json setting specifying a single lower-layer framework's `name`, `version` and optional `rollForwardOnNoCandidateFx` and `applyPatches`.

The normal hierarchy processing for most knobs, such as `rollForwardOnNoCandidateFx`:
 - a) Default value determined by the framework (e.g. roll-forward on [Minor])
 - b) Environment variable override (e.g. `DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX`)
 - c) Each layer's `runtimeOptions` override setting in its runtimeconfig.json, starting with app (e.g. `rollForwardOnNoCandidateFx`). Lower layers can override this.
 - d) The app's `frameworks` section in `[appname].runtimeconfig.json` which allows knobs per-framework.
 - e) A `--` command line value such as `--roll-forward-on-no-candidate-fx`

In a hypothetical example, `Microsoft.AspNetCore.App` references version `2.2.0` of `Microsoft.NetCore.App` in `Microsoft.AspNetCore.App.runtimeconfig.json`:
```json
{
    "runtimeOptions": {
        "framework": {
            "name": "Microsoft.NetCore.App",
            "version": "2.2.0"
        },
     }
}
```
However, if the app requires the newer version `2.2.1` of `Microsoft.NetCore.App`, then mechanism `d` is used. An example of the `frameworks` section for mechanism `d` in the app's `runtimeconfig.json`:
```json
{
    "runtimeOptions": {
        "framework": {
            "name": "Microsoft.AspNetCore.All",
            "version": "1.0.1"
        },
        "frameworks": [
            {
                "name": "Microsoft.AspNetCore.App",
                "version": "2.2.1",
            }
        ]
    }
}
```
