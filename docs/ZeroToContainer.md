# Using the NuGet package to do an end-to-end container build

This guidance will track the most up-to-date version of the package and tasks.
You should expect it to shrink noticeably over time!

## Prerequisites

* [.NET SDK 7.0.100-preview.7](https://dotnet.microsoft.com/download/dotnet/7.0) or higher
* Docker should be installed and running

## Usage

```bash
# create a new project and move to its directory
dotnet new web -n my-awesome-container-app
cd my-awesome-container-app

# add a reference to the package
dotnet add package Microsoft.NET.Build.Containers

# publish your project
dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer

# run your app
docker run -it --rm -p 5010:80 my-awesome-container-app:1.0.0
```

Now you can go to `localhost:5010` and you should see the `Hello World!` text!
