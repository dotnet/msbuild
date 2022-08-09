# Using the generated package to do an end-to-end container build

This guidance will track the most up-to-date version of the package and tasks.
You should expect it to shrink noticeably over time!

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
    --name rainer --username '%GITHUB_USERNAME%' --password '%GITHUB_TOKEN%' \
    --store-password-in-clear-text --configfile nuget.config

# add a reference to the package
dotnet add package Microsoft.NET.Build.Containers --prerelease

# publish your project
dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer

# run your app
docker run -it --rm my-awesome-container-app:latest
```

