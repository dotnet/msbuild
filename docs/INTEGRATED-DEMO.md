# Using the generated package to do an end-to-end container build

This guidance will track the most up-to-date version of the package and tasks.
You should expect it to shrink noticeably over time!

## Prerequisites

* [.NET SDK 7.0.100-preview.7](https://dotnet.microsoft.com/download/dotnet/7.0) or higher
* Docker should be installed and running
* On Windows, Docker must be [configured for Linux containers](https://docs.microsoft.com/virtualization/windowscontainers/quick-start/quick-start-windows-10-linux)
* You should have an environment variable called GITHUB_USERNAME, with your github username in it
* You should have an environment variable called GITHUB_TOKEN, with a github [personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) that has `read:packages` permissions.


## Usage

```bash
# create a new project and move to its directory
dotnet new web -n my-awesome-container-app
cd my-awesome-container-app

# create a nuget.config file to store the configuration for this repo
dotnet new nugetconfig

# add a source for a github nuget registry, so that we can install the package.
# this relies on the GITHUB_USERNAME and
# GITHUB_TOKEN environment variables being present, the token should have 'read:packages'
# permissions. (replace the \ with ` if using powershell)
dotnet nuget add source https://nuget.pkg.github.com/rainersigwald/index.json \
    --name rainer --username "%GITHUB_USERNAME%" --password "%GITHUB_TOKEN%" \
    --store-password-in-clear-text --configfile nuget.config

# add a reference to the package
dotnet add package Microsoft.NET.Build.Containers

# publish your project
dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer

# run your app
docker run -it --rm -p 5010:80 my-awesome-container-app:1.0.0
```

Now you can go to `localhost:5010` and you should see the `Hello World!` text!

