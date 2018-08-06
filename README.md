# .NET Command Line Interface

[![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command and the documentation.

Looking for the .NET Core SDK tooling?
----------------------------------------

If you are looking for the latest nightly of the .NET Core SDK, see https://github.com/dotnet/core-sdk.

Found an issue?
---------------
You can consult the [Documents Index](Documentation/README.md) to find out the current issues and to see the workarounds.

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/project-docs/issue-filing-guide.md) to help you. Please consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

Build Status
------------

|Windows x64 |
|:------:|
|[![](https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/7716/badge)](https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=7716)|

Basic usage
-----------

When you have the .NET Command Line Interface installed on your OS of choice, you can try it out using some of the samples on the [dotnet/core repo](https://github.com/dotnet/core/tree/master/samples). You can download the sample in a directory, and then you can kick the tires of the CLI.


First, you will need to restore the packages:

    dotnet restore

This will restore all of the packages that are specified in the project.json file of the given sample.

Then you can either run from source or compile the sample. Running from source is straightforward:

    dotnet run

Compiling to IL is done using:

    dotnet build

This will drop an IL assembly in `./bin/[configuration]/[framework]/[binary name]`
that you can run using `dotnet bin/[configuration]/[framework]/[binaryname.dll]`.

For more details, refer to the [documentation](https://aka.ms/dotnet-cli-docs).

Building from source
--------------------

If you are building from source, take note that the build depends on NuGet packages hosted on MyGet, so if it is down, the build may fail. If that happens, you can always see the [MyGet status page](http://status.myget.org/) for more info.

Read over the [contributing guidelines](CONTRIBUTING.md) and [developer documentation](Documentation) for prerequisites for building from source.

Questions & Comments
--------------------

For all feedback, use the Issues on this repository.

License
-------

By downloading the .zip you are agreeing to the terms in the project [EULA](https://aka.ms/dotnet-core-eula).


