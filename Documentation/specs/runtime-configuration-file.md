# Runtime Configuration Files

The runtime configuration files store the dependencies of an application (formerly stored in the `.deps` file). They also include runtime configuration options, such as the Garbage Collector mode. Optionally they can also include data for runtime compilation (compilation settings used to compile the original application, and reference assemblies used by the application).

**Note:** This document doesn't provide full explanations as to why individual items are needed in this file. That is covered in the [`corehost` spec](corehost.md) and via the `Microsoft.Extensions.DependencyModel` assembly.

## What produces the files and where are they?

There are two runtime configuration files for a particular application. Given a project named `MyApp`, the compilation process produces the following files (on Windows, other platforms are similar):

* `MyApp.dll` - The managed assembly for `MyApp`, including an ECMA-compliant entry point token.
* `MyApp.exe` - A copy of the `corehost.exe` executable.
* `MyApp.runtimeconfig.json` - An **optional** configuration file containing runtime configuration settings.
* `MyApp.deps.json` - A list of dependencies, as well as compilation context data and compilation dependencies. Not technically required, but required to use the servicing or package cache/shared package install features.

The `MyApp.runtimeconfig.json` is designed to be user-editable (in the case of an app consumer wanting to change various CLR runtime options for an app, much like the `MyApp.exe.config` XML file works in .NET 4.x today). However, the `MyApp.deps.json` file is designed to be processed by automated tools and should not be user-edited. Having the files as separate makes this clearer. We could use a different format for the deps file, but if we're already integrating a JSON parser into the host, it seems most appropriate to re-use that here. Also, there are diagnostic benefits to being able to read the `.deps.json` file in a simple text editor.

**IMPORTANT**: Portable Applications, i.e. those published without a specific RID, have some adjustments to this spec which is covered at the end.

## File format

The files are both JSON files stored in UTF-8 encoding. Below are sample files. Note that not all sections are required and some will be opt-in only (see below for more details). The `.runtimeconfig.json` file is completely optional, and in the `.deps.json` file, only the `runtimeTarget`, `targets` and `libraries` sections are required (and within the `targets` section, only the runtime-specific target is required).

### [appname].runtimeconfig.json
```json
{
    "runtimeOptions": {
        "gcServer": true,
        "gcConcurrent": false,
        "framework": {
            "name": "Microsoft.DotNetCore",
            "version": "1.0.1"
        }
    }
}
```

### [appname].deps.json
```json
{
    "runtimeTarget": ".NETStandardApp,Version=v1.5/osx.10.10-x64",
    "compilationOptions": {
        "defines": [ "DEBUG" ]
    },
    "targets": {
        ".NETStandardApp,Version=v1.5": {
            "MyApp/1.0": {
                "type": "project",
                "dependencies": {
                    "AspNet.Mvc": "1.0.0"
                }
            },
            "System.Foo/1.0.0": {
                "type": "package",
            },
            "System.Banana/1.0.0": {
                "type": "package",
                "dependencies": {
                    "System.Foo": "1.0.0"
                },
                "compile": {
                    "ref/dotnet5.4/System.Banana.dll": { }
                }
            }
        },
        ".NETStandardApp,Version=v1.5/osx.10.10-x64": {
            "MyApp/1.0": {
                "type": "project",
                "dependencies": {
                    "AspNet.Mvc": "1.0.0"
                }
            },
            "System.Foo/1.0.0": {
                "type": "package",
                "runtime": {
                    "lib/dnxcore50/System.Foo.dll": { }
                }
            },
            "System.Banana/1.0.0": {
                "type": "package",
                "dependencies": {
                    "System.Foo": "1.0.0"
                },
                "runtime": {
                    "lib/dnxcore50/System.Banana.dll": { }
                },
                "resources": {
                    "lib/dnxcore50/fr-FR/System.Banana.resources.dll": { "locale": "fr-FR" }
                },
                "native": {
                    "runtimes/osx.10.10-x64/native/libbananahelper.dylib": { }
                }
            }
        }
    },
    "libraries": {
        "MyApp/1.0": {
            "type": "project"
        },
        "System.Foo/1.0": {
            "type": "package",
            "serviceable": true,
            "sha512": "[base64 string]"
        },
        "System.Banana/1.0": {
            "type": "package",
            "sha512": "[base64 string]"
        }
    }
}
```

## Sections

### `runtimeOptions` Section (`.runtimeconfig.json`)

This section is copied verbatim from an identical section in the input `project.json` file (with the exception of the `target` parameter which is generated by the compilation process). The `runtimeConfig` section specifies parameters to be provided to the runtime during initialization. Known parameters include:

* `gcServer` - Boolean indicating if the server GC should be used (Default: _TBD_). Note: This is designed to mirror the existing [app.config](https://msdn.microsoft.com/en-us/library/ms229357.aspx) setting)
* `gcConcurrent` - Boolean indicating if background garbage collection should be used (Default: _TBD_). Note: This is designed to mirror the existing [app.config](https://msdn.microsoft.com/en-us/library/yhwwzef8.aspx) setting).
* `framework` - Indicates the name and version of the shared framework to use when activating the application. The presence of this section indicates that the application is a portable app designed to use a shared redistributable framework.
* Others _TBD_

These settings are read by `corehost` to determine how to initialize the runtime. All versions of `corehost` **must ignore** settings in this section that they do not understand (thus allowing new settings to be added in later versions).

### `compilationOptions` Section (`.deps.json`)

This section is copied by storing the merged `compilationOptions` from the input `project.json`. The `project.json` can define three sets of compilation options: Global, Per-Configuration, and Per-Framework. However, the `[appname].runtimeconfig.json` is specific to a configuration and framework so there is only one merged section here.

The exact settings found here are specific to the compiler that produced the original application binary. Some example settings include: `defines`, `languageVersion` (C#/VB), `allowUnsafe` (C#), etc.

As an example, here is a possible `project.json` file:

```json
{
    "compilationOptions": {
        "allowUnsafe": true
    },
    "frameworks": {
        "net451": {
            "compilationOptions": {
                "defines": [ "DESKTOP_CLR" ]
            }
        },
        "dnxcore50": {
            "compilationOptions": {
                "defines": [ "CORE_CLR" ]
            }
        }
    },
    "configurations": {
        "Debug": {
            "compilationOptions": {
                "defines": [ "DEBUG_MODE" ]
            }
        }
    }
}
```

When this project is built for `dnxcore50` in the `Debug` configuration, the outputted `MyApp.deps.json` file will have the following `compilationOptions` section:

```json
{
    "compilationOptions": {
        "allowUnsafe": true,
        "defines": [ "CORE_CLR", "DEBUG_MODE" ]
    }
}
```

### `runtimeTarget` Section (`.deps.json`)

This property contains the name of the target from `targets` that should be used by the runtime. This is present to simplify `corehost` so that it does not have to parse or understand target names and the meaning thereof.

### `targets` Section (`.deps.json`)

This section contains subsetted data from the input `project.lock.json`.

Each property under `targets` describes a "target", which is a collection of libraries required by the application when run or compiled in a certain framework and platform context. A target **must** specify a Framework name, and **may** specify a Runtime Identifier. Targets without Runtime Identifiers represent the dependencies and assets used for compiling the application for a particular framework. Targets with Runtime Identifiers represent the dependencies and assets used for running the application under a particular framework and on the platform defined by the Runtime Identifier. In the example above, the `.NETStandardApp,Version=v1.5` target lists the dependencies and assets used to compile the application for `dnxcore50`, and the `.NETStandardApp,Version=v1.5/osx.10.10-x64` target lists the dependencies and assets used to run the application on `dnxcore50` on a 64-bit Mac OS X 10.10 machine.

There will always be two targets in the `[appname].runtimeconfig.json` file: A compilation target, and a runtime target. The compilation target will be named with the framework name used for the compilation (`.NETStandardApp,Version=v1.5` in the example above). The runtime target will be named with the framework name and runtime identifier used to execute the application (`.NETStandardApp,Version=v1.5/osx.10.10-x64` in the example above). However, the runtime target will also be identified by name in the `runtimeOptions` section, so that `corehost` need not parse and understand target names.

The content of each target property in the JSON is a JSON object. Each property of that JSON object represents a single dependency required by the application when compiled for/run on that target. The name of the property contains the ID and Version of the dependency in the form `[Id]/[Version]`. The content of the property is another JSON object containing metadata about the dependency.

The `type` property of a dependency object defines what kind of entity satisfied the dependency. Possible values include `project` and `package` (further comments on dependency types below). 

**Open Question:** `type` is also present in the `libraries` section. We don't really need it in both. It's in both now because the lock file does that and we want the formats to be similar. Should we remove it? 

The `dependencies` property of a dependency object defines the ID and Version of direct dependencies of this node. It is a JSON object where the property names are the ID of the dependency and the content of each property is the Version of the dependency.

The `runtime` property of a dependency object lists the relative paths to Managed Assemblies required to be available at runtime in order to satisfy this dependency. The paths are relative to the location of the Dependency (see below for further details on locating a dependency).

The `resources` property of a dependency object lists the relative paths and locales of Managed Satellite Assemblies which provide resources for other languages. Each item contains a `locale` property specifying the [IETF Language Tag](https://en.wikipedia.org/wiki/IETF_language_tag) for the satellite assembly (or more specifically, a value usable in the Culture field for a CLR Assembly Name).

The `native` property of a dependency object lists the relative paths to Native Libraries required to be available at runtime in order to satisfy this dependency. The paths are relative to the location of the Dependency (see below for further details on locating a dependency).

In compilation targets, the `runtime`, `resources` and `native` properties of a dependency are omitted, because they are not relevant to compilation. Similarly, in runtime targets, the `compile` property is omitted, because it is not relevant to runtime.

Only dependencies with a `type` value of `package` should be considered by `corehost`. There may be other items, used for other purposes (for example, Projects, Reference Assemblies, etc.

### `libraries` Section (`.deps.json`)

This section contains a union of all the dependencies found in the various targets, and contains common metadata for them. Specifically, it contains the `type`, as well as a boolean indicating if the library can be serviced (`serviceable`, only for `package`-typed libraries) and a SHA-512 hash of the package file (`sha512`, only for `package`-typed libraries.

## How the file is used

The file is read by two different components:

* `corehost` uses it to determine what to place on the TPA and Native Library Search Path lists, as well as what runtime settings to apply (GC type, etc.). See [the `corehost` spec](corehost.md).
* `Microsoft.Extensions.DependencyModel` uses it to allow a running managed application to query various data about it's dependencies. For example:
  * To find all dependencies that depend on a particular package (used by ASP.NET MVC and other plugin-based systems to identify assemblies that should be searched for possible plugin implementations)
  * To determine the reference assemblies used by the application when it was compiled in order to allow runtime compilation to use the same reference assemblies (used by ASP.NET Razor to compile views)
  * To determine the compilation settings used by the application in order to allow runtime compilation to use the same settings (also used by ASP.NET Razor views).
  
## Opt-In Compilation Data

Some of the sections in the `.deps.json` file contain data used for runtime compilation. This data is not provided in the file by default. Instead, a project.json setting `preserveCompilationContext` must be set to true in order to ensure this data is added. Without this setting, the `compilationOptions` will not be present in the file, and the `targets` section will contain only the runtime target. For example, if the `preserveCompilationContext` setting was not present in the `project.json` that generated the above example, the `.deps.json` file would only contain the following content:

```json
{
    "runtimeTarget": {
        "name": ".NETStandardApp,Version=v1.5/osx.10.10-x64",
        "portable": false
    },
    "targets": {
        ".NETStandardApp,Version=v1.5/osx.10.10-x64": {
            "MyApp/1.0": {
                "dependencies": {
                    "AspNet.Mvc": "1.0.0"
                }
            },
            "System.Foo/1.0.0": {
                "runtime": {
                    "lib/dnxcore50/System.Foo.dll": { }
                }
            },
            "System.Banana/1.0.0": {
                "dependencies": {
                    "System.Foo": "1.0.0"
                },
                "runtime": {
                    "lib/dnxcore50/System.Banana.dll": { }
                },
                "resources": {
                    "lib/dnxcore50/fr-FR/System.Banana.resources.dll": { "locale": "fr-FR" }
                },
                "native": {
                    "runtimes/osx.10.10-x64/native/libbananahelper.dylib": { }
                }
            }
        }
    },
    "libraries": {
        "MyApp/1.0": {
            "type": "project"
        },
        "System.Foo/1.0": {
            "type": "package",
            "serviceable": true,
            "sha512": "[base64 string]"
        },
        "System.Banana/1.0": {
            "type": "package",
            "sha512": "[base64 string]"
        }
    }
}
```

## Portable Deployment Model

An application can be deployed in a "portable" deployment model. In this case, the RID-specific assets of packages are published within a folder structure that preserves the RID metadata. However, `corehost` does not use this folder structure, rather it reads data from the `.deps.json` file. Also, during deployment, the `.exe` file (`corehost` renamed) is not deployed.

In the portable deployment model, the `*.runtimeConfig.json` file will contain the `runtimeOptions.framework` section:

```json
{
    "runtimeOptions": {
        "framework": {
            "name": "NETCore.App",
            "version": "1.0.1"
        }
    }
}
```

This data is used to locate the shared framework folder. The exact mechanics of which version are selected are defined elsewhere, but in general, it locates the shared runtime in the `shared` folder located beside it by using the relative path `shared/[runtimeOptions.framework.name]/[runtimeOptions.framework.version]`. Once it has applied any version roll-forward logic and come to a final path to the shared framework, it locates the `[runtimeOptions.framework.name].deps.json` file within that folder and loads it **first**.

Next, the deps file from the application is loaded and merged into this deps file (this is conceptual, the host implementation doesn't necessary have to directly merge the data ;)). Data from the app-local deps file trumps data from the shared framework.

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

The host will have a RID embedded in it during compilation (for example, `win10-x64` for Windows 64-bit). It will look up the corresponding entry in the `runtimes` section to identify what the fallback list is for `win10-x64`. The fallbacks are identified from most-specific to least-specific. In the case of `win10-x64` and the example above, the fallback list is: `"win10-x64", "win10", "win81-x64", "win81", "win8-x64", "win8", "win7-x64", "win7", "win-x64", "win", "any", "base"` (note that an exact match on the RID itself is the first preference, followed by the first item in the fallback list, then the next item, and so on).

In the app-local deps file for a `portable` application, the package entries may have an additional `runtimeTargets` section detailing RID-specific assets. The `corehost` application should use this data, along with the current RID and the RID fallback data defined in the `runtimes` section of the shared framework deps file to select one **and only one** RID value out of each package individually. The most specific RID present within the package should always be selected.

Consider `corehost` built for `ubuntu.14.04-x64` and the following snippet from an app-local deps file (some sections removed for brevity).

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
                        "rid": "unix"
                    },
                    "runtimes/win7-x64/lib/netstandard1.5/System.Data.SqlClient.dll": {
                        "assetType": "runtime",
                        "rid": "win7-x64"
                    },
                    "runtimes/win7-x86/lib/netstandard1.5/System.Data.SqlClient.dll": {
                        "assetType": "runtime",
                        "rid": "win7-x86"
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
