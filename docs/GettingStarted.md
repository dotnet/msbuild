# Get started

## Get the package

If you're on .NET SDK 7.0.200 or up and you're a Web SDK project, then you can just set the `EnableSdkContainerSupport` property to `true` in your project file. Otherwise, you'll need to add the latest version of the `Microsoft.NET.Build.Containers` package. You can do this with the .NET CLI via the following command:


```shell
>dotnet add package Microsoft.NET.Build.Containers
```

## Build a container

Now that you've got the package, build a default container for your application via the following command:

```shell
>dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer
...
Pushed container '<your app name>:<your app version>' to registry 'docker://'
...
```

That's all it takes! You can customize the behavior of the tools using MSBuild properties, often put into a separate Publish Profile. For more information, see [Publish Profiles](https://docs.microsoft.com/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-6.0#publish-profiles).

The `Microsoft.NET.Build.Containers` package infers a number of properties about the generated container image:

* Which base image to use.
* Which version of the base image to use.
* Where to push the generated image.

For more information, see [Customizing a container](./ContainerCustomization.md)


> **Note**
> If you are publishing a console application (or any non-Web project) you will need to add the `/t:PublishContainer` option to the command line above. See [dotnet/sdk-container-builds#141](https://github.com/dotnet/sdk-container-builds/issues/141) for more details.