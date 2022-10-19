# Get started

Run the following commands to build a container from the SDK: 

```shell
>dotnet add package Microsoft.NET.Build.Containers --prerelease
>dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer
...
Pushed container '<your app name>:<your app version>' to registry 'docker://'
...
```

For more information, see [Publish Profiles](https://docs.microsoft.com/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-6.0#publish-profiles).

The `Microsoft.NET.Build.Containers` package infers a number of properties about the generated container image:

* Which base image to use.
* Which version of the base image to use.
* Where to push the generated image.

<!--The `Microsoft.NET.Build.Containers` package provides full control over the preceding properties. -->
For more information, see [Customizing a container](./ContainerCustomization.md)

> **Note**
> This package only supports Linux containers in this version.

> **Note**
> This package only supports Web projects (those that use the `Microsoft.NET.Sdk.Web` SDK) in this version.
