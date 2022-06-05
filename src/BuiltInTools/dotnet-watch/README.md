dotnet-watch
============
`dotnet-watch` is a file watcher for `dotnet` that restarts the specified application when changes in the source code are detected.

### How To Use

The command must be executed in the directory that contains the project to be watched.

    Usage: dotnet watch [options] [[--] <args>...]

    Options:
      -q, --quiet                             Suppresses all output except warnings and errors
      -v, --verbose                           Show verbose output
      --no-hot-reload                         Suppress hot reload for supported apps.
      --non-interactive                       Runs dotnet-watch in non-interactive mode. This option is only supported when running with
                                              Hot Reload enabled. Use this option to prevent console input from being captured.
      --project <project>                     The project to watch
      -lp, --launch-profile <launch-profile>  The launch profile to start the project with. This option is only supported when running
                                              'dotnet watch' or 'dotnet watch run'.
      --list                                  Lists all discovered files without starting the watcher.

Add `watch` after `dotnet` and before the command arguments that you want to run:

| What you want to run                           | Dotnet watch command                                     |
| ---------------------------------------------- | -------------------------------------------------------- |
| dotnet run                                     | dotnet **watch** run                                     |
| dotnet run --arg1 value1                       | dotnet **watch** run --arg1 value                        |
| dotnet run --framework net451 -- --arg1 value1 | dotnet **watch** run --framework net451 -- --arg1 value1 |
| dotnet test                                    | dotnet **watch** test                                    |

### Environment variables

Some configuration options can be passed to `dotnet watch` through environment variables. The available variables are:

| Variable                                       | Effect                                                   |
| ---------------------------------------------- | -------------------------------------------------------- |
| DOTNET_USE_POLLING_FILE_WATCHER                | If set to "1" or "true", `dotnet watch` will use a polling file watcher instead of CoreFx's `FileSystemWatcher`. Used when watching files on network shares or Docker mounted volumes.                       |
| DOTNET_WATCH_SUPPRESS_MSBUILD_INCREMENTALISM   | By default, `dotnet watch` optimizes the build by avoiding certain operations such as running restore or re-evaluating the set of watched files on every file change. If set to "1" or "true",  these optimizations are disabled. |
| DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER   | `dotnet watch run` will attempt to launch browsers for web apps with `launchBrowser` configured in `launchSettings.json`. If set to "1" or "true", this behavior is suppressed. |
| DOTNET_WATCH_SUPPRESS_MSBUILD_INCREMENTALISM   | `dotnet watch run` will attempt to refresh browsers when it detects file changes. If set to "1" or "true", this behavior is suppressed. This behavior is also suppressed if DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER is set. |
| DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING | If set to "1", or "true", `dotnet watch` will not perform special handling for static content file

### MSBuild

dotnet-watch can be configured from the MSBuild project file being watched.

**Watch items**

dotnet-watch will watch all items in the **Watch** item group.
By default, this group inclues all items in **Compile** and **EmbeddedResource**.

More items can be added to watch in a project file by adding items to 'Watch'.

```xml
<ItemGroup>
    <!-- extends watching group to include *.js files -->
    <Watch Include="**\*.js" Exclude="node_modules\**\*.js;$(DefaultExcludes)" />
</ItemGroup>
```

dotnet-watch will ignore Compile and EmbeddedResource items with the `Watch="false"` attribute.

Example:

```xml
<ItemGroup>
    <!-- exclude Generated.cs from dotnet-watch -->
    <Compile Update="Generated.cs" Watch="false" />
    <!-- exclude Strings.resx from dotnet-watch -->
    <EmbeddedResource Update="Strings.resx" Watch="false" />
</ItemGroup>
```

**Project References**

By default, dotnet-watch will scan the entire graph of project references and watch all files within those projects.

dotnet-watch will ignore project references with the `Watch="false"` attribute.

```xml
<ItemGroup>
  <ProjectReference Include="..\ClassLibrary1\ClassLibrary1.csproj" Watch="false" />
</ItemGroup>
```


**Advanced configuration**

dotnet-watch performs a design-time build to find items to watch.
When this build is run, dotnet-watch will set the property `DotNetWatchBuild=true`.

Example:

```xml
  <ItemGroup Condition="'$(DotNetWatchBuild)'=='true'">
    <!-- only included in the project when dotnet-watch is running -->
  </ItemGroup>
```

### Hot Reload

Starting in .NET 6, dotnet-watch adds support for hot reload. Hot reload is a set of technologies that allows applying changes to a running app without having to rebuild it. This enables a much faster development experience as developers receive immediate feedback when modifying their apps during local development.

When a file is modified, watch determines if it can be hot reloaded. Here's how different kinds of files are handled:

#### Static assets (.css, .js, .jpg etc)
In a webapp, static web assets such as css, js, or images can be reloaded by notifying the browser and having it re-fetch the asset. CSS files in particular can be replaced in-place without having to reload the page, thus preserving user state.

To perform the refresh, dotnet-watch needs to communicate to the browser and give it instructions such as refresh the script or reload the browser. It hosts a WebSocket server in process that can send these commands. On the other end, dotnet-watch
needs a piece of JavaScript that runs as part of the app's rendered HTML output to process these commands. When starting up the app, dotnet-watch configures it so that it can inject a ASP.NET Core middleware in to the app's middleware pipeline. It uses a combination of [startup hooks](https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-startup-hook.md) and [startup filters](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/startup?view=aspnetcore-5.0#extend-startup-with-startup-filters) to perform this injection. The middleware modifies any HTML responses returned by the app to reference a JavaScript script file:

```html
<h1>My cool website</h1>
...

<!-- The script that follows is injected by the middleware right before the closing body tag -->
<script src="_framework/aspnetcore-browser-refresh.js"></script>
</body>
```

Watch uses yet another environment variable to tell the app / middleware what the URL of the server is that the injected script needs to communicate with. aspnetcore-browser-refresh.js connects to this URL and is now ready to listen to process dotnet-watch's reload commands. Prior to .NET 6.0, commands were simple strings such as 'Reload' which would cause the browser to reload or 'Wait' which would indicate that the dotnet-watch is in the process of rebuilding. Starting in 6.0, these messages also include complex JSON payloads that contain additional data. For instance, the command to update a static asset looks like:

```json
{
  "type": "UpdateStaticFile",
  "path": "wwwroot/css/site.css"
}
```

The implementation for the static asset reload is heavily inspired by @RickStrah's Live Reload middleware located at https://github.com/RickStrahl/Westwind.AspnetCore.LiveReload

Scoped css files (.razor.css and .cshtml.css) are a special case of static web assets. They need to be processed (currently requires running MSBuild targets) to be updated. Once updated, the mechanics of updating are identical to other static assets.

#### Compiled files (.cs, .razor, .cshtml)

Updating a compiled file requires two requirements -

1. the compiler needs to able to produce a binary patch from the source changes
1. The app needs to be able to apply the binary patch.
1. Have the app state update to reflect the result of the binary patch.

The former has been available as a compiler feature to support debugger / IDE's Edit and Continue capabilities. Starting in 6.0, the runtime (.NET Core and Mono) [expose APIs](https://github.com/dotnet/runtime/blob/f15722c2c25ce945f2f1c7673ff3f4fbdb244feb/src/mono/System.Private.CoreLib/src/System/Reflection/Metadata/AssemblyExtensions.cs#L28) to patch a running app. The combination of the two makes it possible to hot reload changes to files that are compiled as part of the application. The first bullet point is easy for files that are already compiled as part of the app e.g. .cs or .vb files. For any other file types, source generators offer a way to participate in the compilation process. This is the route taken by Razor Source Generator for .razor and .cshtml files.

To enable this, dotnet-watch hosts a Roslyn workspace that contains the project and all of it's references. When a change to .cs, .razor, or .cshtml file is found, it updates the workspace and uses it's APIs to produce a patch. Not all source changes can however produce a patch. These are called rude-edits (since it rudely interrupts your workflow) and in this case, watch falls back to doing a regular build to update and restart the app. If a patch is successfuly produced, watch now has the task of sending these patches to the app. In a webapp, we're able to display these rude-edit diagnostics, as well as any compilation errors using the WebSocket connection that was previously discussed.

Since different app models have different constraints, this implementation is app-model specific:

* For .NET Core apps, dotnet-watch hosts a NamedPipe server. It once again uses a startup hook to inject code (`Microsoft.Extensions.DotNetDeltaApplier`) in to the app that creates a client to listen to this NamedPipe. Each successful hot reload results in a binary payload with the following format:

```
[Version: byte | Currently 0]
[Absolute path of file changed: string]
[Number of deltas produced: int32]
[Delta item 1]
[Delta item 2]
...
[Delta item n]
```

where each delta item looks like so
```
[ModuleId: string]
[MetadataDelta byte count: int32]
[MetadataDelta bytes]
[ILDelta byte count: int32]
[ILDelta bytes]
```

The client reads this payload, and calls `AssemblyExtensions.ApplyUpdate`. The `ModuleId` in the delta corresponds to `Module.ModuleVersionId` on an Assembly. In general, each .NET assembly only have one module so there's a one-to-one relation here.

An interesting side-effect of adding a startup hook to the launched app is that it makes using `dotnet run` a little trickier. The dotnet CLI is also a managed process and inherits the STARTUP_HOOK for the duration the `run` command executes. To avoid issues here, under hot reload, dotnet-watch emulates the `run` command rather than shelling out to it.

* For Blazor WebAssembly apps, dotnet-watch piggybacks on the same WebSocket connection that it established with the browser to send the payload. The payload format is similar, but this time it's a JSON blob.

```json
{
  "type": "BlazorHotReloadDeltav1",
  "deltas": [
    {
      "moduleId": "<Guid>",
      "metadataDelta": "<base64-encoded-bytes>",
      "ilDelta": "<base64-encoded-bytes>"
    }
  ]
}
```

Blazor WebAssembly's implementation [bakes in an API](https://github.com/dotnet/aspnetcore/blob/bb3bdd76a2601c6ed2a118343788b7eef2ebdd62/src/Components/WebAssembly/WebAssembly/src/HotReload/WebAssemblyHotReload.cs) available via [JSInterop](https://docs.microsoft.com/en-us/aspnet/core/blazor/call-dotnet-from-javascript?view=aspnetcore-5.0) that can receive and apply these deltas.

Once the delta is applied, the app state needs to be updated to reflect the new state. To support this, the runtime exposes a new assembly-level attribute https://github.com/dotnet/runtime/blob/5297337e5b7db9feef81f612f3d4e70128c7fa55/src/libraries/System.Private.CoreLib/src/System/Reflection/Metadata/MetadataUpdateHandlerAttribute.cs. The agent code (assembly injected by the startup hook), discovers types annotated with these attributes and invokes methods on them via reflection. Currently the following methods are called:

```C#
static void ClearCache(Type[]?);
static void UpdateApplication(Type[]?);
```
`ClearCache` allows runtime and library code to clear any reflection-based caches. This allows applications to "renew" their state based on new information produced by the delta. This includes caches such as System.Text.Json's caches about types being serialized, Blazor's caches about what properties are parameters, MVC's caches about controllers, actions and models, etc. `UpdateApplication` allows any state (primarily UI) to be updated. For e.g. in a Blazor or WinForms app, this causes the UI to re-rendered.

## Contribution

Follow the contribution steps for the dotnet SDK: /documentation/project-docs/developer-guide.md. If developing from Visual Studio, open the dotnet-watch.slnf.
