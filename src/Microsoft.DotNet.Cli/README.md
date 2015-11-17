dotnet
======

**NAME**

dotnet -- general driver for running the command-line commands

**SYNOPSIS**

dotnet [common-options] [command] [arguments]

**DESCRIPTION**
dotnet is a generic driver for the CLI toolchain. Invoked on its own, it will give out brief usage instructions. 

Each specific feature is implemented as a command. In order to use the feautre, it is specified after dotnet, i.e. dotnet compile. All of the arguments following the command are command's own arguments.  


**Arguments**
-v, --verbose
Enable verbose output

--version
Print out the version of the CLI tooling

**Commands**

There are many possible commands that you can use. The few main ones are:
* run - run your code from source
* compile - compile your source code
* publish - publish
  

**SEE ALSO**
dotnet-compile
dotnet-run
dotnet-publish
