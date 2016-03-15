Known issues & workarounds
==========================

## Resolving the Standard library packages
The StdLib package is on a MyGet feed. In order to restore it, a MyGet feed needs to be added 
to the NuGet feeds, either locally per application or in a central location. 

**Issues tracking this:** 

* [#535](https://github.com/dotnet/cli/issues/535)

**Affects:** `dotnet restore`

**Workaround:** update to the latest bits and run `dotnet new` in an empty directory. This will 
now drop a `nuget.config` file that you can use in other applications. 

If you cannot update, you can use the following `nuget.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="dotnet-core" value="https://dotnet.myget.org/F/dotnet-core/api/v3/index.json" />
    <add key="api.nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## Uninstalling/reinstalling the PKG on OS X
OS X doesn't really have an uninstall capacity for PKGs like Windows has for 
MSIs. There is, however, a way to remove the bits as well as the "recipe" for 
dotnet. More information can be found on [this SuperUser question](http://superuser.com/questions/36567/how-do-i-uninstall-any-apple-pkg-package-file).

# What is this document about? 
This document outlines the known issues and workarounds for the current state of 
the CLI tools. Issues will also have a workaround and affects sections if necessary. You can use this page to 
get information and get unblocked.

# What is a "known issue"?
A "known issue" is a major issue that block users in doing their everyday tasks and that affect all or 
most of the commands in the CLI tools. If you want to report or see minor issues, you can use the [issues list](https://github.com/dotnet/cli/issues). 

  
