# Customizing your container

You can control many aspects of the generated container through MSBuild properties. In general, if you could use a command in a Dockerfile to set some configuration, you can do the same via MSBuild. 

> **Note**
> The only exception to this is `RUN` commands - due to the way we build containers, those cannot be emulated. If you need this functionality, you will need to use a Dockerfile to build your container images.

> **Note**
> This package only supports Linux containers in this version.

## ContainerBaseImage

This property controls the image used as the basis for your image. By default, we will infer the following values for you based on the properties of your project:

* if your project is self-contained, we use the `mcr.microsoft.com/dotnet/runtime-deps` image as the base image
* if your project is an ASP.NET Core project, we use the `mcr.microsoft.com/dotnet/aspnet` image as the base image
* otherwise we use the `mcr.microsoft.com/dotnet/runtime` image as the base image

We infer the tag of the image to be the numeric component of your chosen `TargetFramework` - so a `.net6.0` project will use the `6.0` tag of the inferred base image, a `.net7.0-linux` project will use the `7.0` tag, and so on.

If you set a value here, you should set the fully-qualified name of the image to use as the base, including any tag you prefer:

```xml
<ContainerBaseImage>mcr.microsoft.com/dotnet/runtime:6.0</ContainerBaseImage>
```

## ContainerRegistry

This property controls the destination registry - the place that the newly-created image will be pushed to.

Be default, we push to the local Docker daemon (annotated by `docker://`), but for this release you can specify any _unauthenticated_ registry. For example:

```xml
<ContainerRegistry>registry.mycorp.com:1234</ContainerRegistry>
```

> **Note**
> There is no authentication currently supported - that [will come in a future release](https://github.com/rainersigwald/containers/issues/70) - so make sure you're pointing to an unauthenticated registry

## ContainerImageName

This property controls the name of the image itself, e.g `dotnet/runtime` or `my-awesome-app`. 

By default, the value used will be the `AssemblyName` of the project.


```xml
<ContainerImageName>my-super-awesome-app</ContainerImageName>
```

> **Note**
> Image names can only contain lowercase alphanumeric characters, periods, underscores, and dashes, and must start with a letter or number - any other characters will result in an error being thrown.

## ContainerImageTag

This property controls the tag that is generated for the image. Tags are often used to refer to different versions of an application, but they can also refer to different operating system distributions, or even just different baked-in configuration.

By default, the value used will be the `Version` of the project.

```xml
<ContainerImageTag>1.2.3-alpha2</ContainerImageTag>
```

> **Note**
> Tags can only contain up to 127 alphanumeric characters, periods, underscores, and dashes. They must start with an alphanumeric character or an underscore. Any other form will result in an error being thrown.

## ContainerWorkingDirectory

This property controls the working directory of the container - the directory that commands are executed within if not other command is run.

By default, we use the `/app` directory as the working directory.

```xml
<ContainerWorkingDirectory>/bin</ContainerWorkingDirectory>
```

## Unsupported properties

There are many other properties and items that we want to add support for in subsequent previews:

* Entrypoints
* Entrypoint Arguments
* Ports
* Environment Variables
* Labels

We expect to add them in future versions, so watch this space!
