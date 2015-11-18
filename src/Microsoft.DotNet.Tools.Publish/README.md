dotnet-publish
==============

**NAME**

dotnet-publish -- packs the application and all of its dependencies into a folder getting it ready for publishing

**SYNOPSIS**

dotnet-publish [options] [project]

**DESCRIPTION**
dotnet-publish will compile the application, read through its dependencies specified in project.json and publish the resulting set of files to a directory. This directory contains the assemblies, the runtime as well as the runnable version of the application. This directory can then be moved to a different machine and the application will be able to be ran regardless of existence of any other runtime.  

dotnet-publish needs access to project.json to work. If it is not specified on invocation via [project], project.json in the current directory will be the default. If no project.json can be found, dotnet-publish will error out. 

The command also requires information on the targeted framework and runtime, both of which can be specified on the command line. If the runtime is not specified, the command will default to the runtime for the current operating system. If the framework is not specified, the command will read the information from the project.json file. In case of no valid framework found, the command will error out. In case of multiple valid frameworks found, the command will publish for all valid frameworks. 


* ubuntu

**Options**

-f, --framework [FID]
Publish the application for a given framework identifier (FID). If not specified, FID is read from project.json

-r, --runtime [RID]
Publish the application for a given runtime. Supported values for runtimes at this time are:
	* ubuntu.14.04-x64
	* win7-x64
	* osx.10.10-x64

-o, --output
Specify the path where to place the directory. If not specified, will default to ./bin/[configuration]/[framework]/[runtime]/

-c, --configuration [Debug|Release]
Configuration to use when publishing. If not specified, will default to "Debug".

  

**EXAMPLES**
dotnet-publish 
	Publish the current application using the project.json framework and runtime for the current operating system. 

dotnet-publish ~/projects/app1/project.json  
	Publish the application using the specified project.json; also use framework specified withing and runtime for the current operating system. 
	
dotnet-publish --framework dnxcore50
	Publish the current application using the dnxcore50 framework and runtime for the current operating system. 
	
dotnet-publish --framework dnxcore50 --runtime osx.10.10-x64
	Publish the current application using the dnxcore50 framework and runtime for OS X 10.10  
	
**SEE ALSO**
dotnet-restore
