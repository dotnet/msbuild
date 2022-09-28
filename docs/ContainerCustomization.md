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
> There is no authentication currently supported - that [will come in a future release](https://github.com/dotnet/sdk-container-builds/issues/70) - so make sure you're pointing to a local Docker daemon

## ContainerImageName

This property controls the name of the image itself, e.g `dotnet/runtime` or `my-awesome-app`. 

By default, the value used will be the `AssemblyName` of the project.


```xml
<ContainerImageName>my-super-awesome-app</ContainerImageName>
```

> **Note**
> Image names can only contain lowercase alphanumeric characters, periods, underscores, and dashes, and must start with a letter or number - any other characters will result in an error being thrown.

## ContainerImageTag(s)

This property controls the tag that is generated for the image. Tags are often used to refer to different versions of an application, but they can also refer to different operating system distributions, or even just different baked-in configuration. This property also can be used to push multiple tags - simply use a semicolon-delimited set of tags in the `ContainerImageTags` property, similar to setting multiple `TargetFrameworks`.

By default, the value used will be the `Version` of the project.

```xml
<ContainerImageTag>1.2.3-alpha2</ContainerImageTag>
```

```xml
<ContainerImageTags>1.2.3-alpha2;latest</ContainerImageTags>
```


> **Note**
> Tags can only contain up to 127 alphanumeric characters, periods, underscores, and dashes. They must start with an alphanumeric character or an underscore. Any other form will result in an error being thrown.

## ContainerWorkingDirectory

This property controls the working directory of the container - the directory that commands are executed within if not other command is run.

By default, we use the `/app` directory as the working directory.

```xml
<ContainerWorkingDirectory>/bin</ContainerWorkingDirectory>
```

## ContainerPort

This item adds TCP or UDP ports to the list of known ports for the container. This enables container runtimes like Docker to map these ports to the host machine automatically. This is often used as documentation for the container, but can also be used to enable automatic port mapping.

ContainerPort items have two properties:
* Include
  * The port number to expose
* Type
  * One of `tcp` or `udp` - the default is `tcp`

```xml
<ItemGroup>
    <ContainerPort Include="80" Type="tcp" />
</ItemGroup>
```

> **Note**
> This item does nothing for the container by default and should be considered advisory at best.

## ContainerLabel

This item adds a metadata label to the container. Labels have no impact on the container at runtime, but are often used to store version and authoring metadata for use by security scanners and other infrastructure tools.

ContainerLabel items have two properties:
* Include
  * The key of the label
* Value
  * The value of the label - this may be empty

```xml
<ItemGroup>
    <ContainerLabel Include="org.contoso.businessunit" Value="contoso-university" />
<ItemGroup>
```

## Unsupported properties

There are many other properties and items that we want to add support for in subsequent previews:

* Entrypoints
* Entrypoint Arguments
* Environment Variables

We expect to add them in future versions, so watch this space!
