# Using the generated package to do an end-to-end container build

This guidance will track the most up-to-date version of the package and tasks.
You should expect it to shrink noticeably over time!

```bash
# Prerequisite - have a local container registry running on port 5010.
# This will go away shortly in favor of pushing to your local docker 
# daemon by default
docker run -d -p 5010:5000 --restart=always --name registry registry:2

# create a new project and move to its directory
dotnet new console -n my-awesome-container-app
cd my-awesome-container-app

# create a nuget.config file to store the configuration for this repo
dotnet new nugetconfig

# add a source for a github nuget registry, so that we can install the package.
# this relies on the GITHUB_USERNAME and
# GITHUB_TOKEN environment variables being present, the token should have 'read:packages'
# permissions. (replace the \ with ` if using powershell)
dotnet nuget add source https://nuget.pkg.github.com/rainersigwald/index.json \
    --name rainer --username '%GITHUB_USERNAME%' --password '%GITHUB_TOKEN%' \
    --store-password-in-clear-text --configfile nuget.config

# add a reference to the package
dotnet add package System.Containers.Tasks --version 0.1.0

# publish your project
dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer `
    -p:ContainerBaseImageName=dotnet/runtime `
    -p:ContainerBaseImageTag=7.0    `
    -p:ContainerEntryPoint="dotnet"
    -p:ContainerEntryPointArgs="/app/my-awesome-container-app.dll"

# run your app
docker run -it --rm localhost:5010/my-awesome-container-app
```

