# Containerize Demo

this is a simple thread to go from zero to a containerized application using the `containerize` app from this repo.

To perform this demo, you must have [Docker](https://www.docker.com/products/docker-desktop/) installed locally.

## Set up your environment

### Create the registry

To containerize your app, we'll be using a local container registry until we get the end-to-end with the local Docker daemon set up. This means we'll need to run and seed that registry with the base images. To do so, run the following commands from the repo root:

```shell
> dotnet build --no-restore -t:StartDockerRegistry
> docker ps
CONTAINER ID   IMAGE        COMMAND                  CREATED        STATUS          PORTS                    NAMES
6900146c8604   registry:2   "/entrypoint.sh /etc…"   17 hours ago   Up 15 seconds   0.0.0.0:5010->5000/tcp   registry
```

You should see the 'registry' container running. This is where your images will be stored.

### Preload base images

Now seed the registry with the required base images:

```shell
> dotnet build --no-restore -t:PreloadBaseImages
```

This command will show a lot of output, and it might take a minute.

### Build the containerize app

```shell
> cd containerize
> dotnet publish containerize
> $env:PATH="$env:PATH;$pwd\\bin\\Debug\\net7.0\\publish\\"
> containerize --help
```

## Create an app

Create a new app in a location of your choice:

```shell
> ➜ dotnet new console -n my-containerized-app
The template "Console App" was created successfully.

Processing post-creation actions...
Restoring C:\Users\chethusk\OSS\Scratch\my-containerized-app\my-containerized-app.csproj:
  Determining projects to restore...
  Restored C:\Users\chethusk\OSS\Scratch\my-containerized-app\my-containerized-app.csproj (in 90 ms).
Restore succeeded.
> cd my-containerized-app
```

now, publish that app for linux-x64:

```shell
> dotnet publish --os linux --arch x64 -p:Version=1.2.3
MSBuild version 17.3.0-preview-22329-01+77c72dd0f for .NET
  Determining projects to restore...
  Restored C:\Users\chethusk\OSS\Scratch\my-containerized-app\my-containerized-app.csproj (in 1.39 sec).
  my-containerized-app -> C:\Users\chethusk\OSS\Scratch\my-containerized-app\bin\Debug\net7.0\linux-x64\my-containerized-app.dll
  my-containerized-app -> C:\Users\chethusk\OSS\Scratch\my-containerized-app\bin\Debug\net7.0\linux-x64\publish\
```

Now, containerize your app:

```shell
> containerize  .\bin\Debug\net7.0\linux-x64\publish\ --entrypoint /app/my-containerized-app --name my-containerized-app
Reading from http://localhost:5010/
Reading manifest for dotnet/runtime:7.0
Copying from C:\Users\chethusk\OSS\Scratch\my-containerized-app\bin\Debug\net7.0\linux-x64\publish\ to /app
Pushed localhost:5010/my-containerized-app:latest
latest: Pulling from my-containerized-app
461246efe0a7: Already exists
2b6a8a95a1cb: Already exists
4bb378e5a440: Already exists
39c4bd5820ec: Already exists
5c433ed2534f: Pull complete
Digest: sha256:d52472a3e0b77e3fae4cf3ffe2ab3a575260c8ffd28c2e9a38b81d070f77fdb9
Status: Downloaded newer image for localhost:5010/my-containerized-app:latest
localhost:5010/my-containerized-app:latest
Loaded image into local Docker daemon. Use 'docker run --rm -it --name my-containerized-app localhost:5010/my-containerized-app:latest' to run the application.
```

The output of the containerize command tells you the docker command to run, so lets do that:

```shell
> docker run --rm -it --name my-containerized-app localhost:5010/my-containerized-app:latest
Hello, World!
```

That's it!
