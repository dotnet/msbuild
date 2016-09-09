@ECHO OFF
SETLOCAL

:: This batch file creates NuGet packages for shipped MSBuild 14.0 assemblies.  It copies the assemblies locally and then builds "reference"
:: assemblies which are empty assemblies used by the compiler during a build.  The reference assembly is built for net45 and netstandard1.3 with
:: certain code #ifdef'd out.  This allows users to compile a single assembly that should work cross-platform

:: To update the packages, change the VERSIONMINOR, VERSIONBUILD, and BUILDDROP variables.  Generally packages should be pushed to the dotnet 
:: NuGet feed until tested and then placed on NuGet.org.

:: The major version here should align with the major version of MSBuild being packaged
SET VERSIONMAJOR=14
:: The minor version here should align with the Visual Studio Update of MSBuild being packaged
SET VERSIONMINOR=3
:: The build number here should be incremented when servicing the NuGet package.  The tag should be prereleaseNN when trying out something new
SET VERSIONBUILD=0
:: This is the path to where this file is located
SET ROOT=%~dp0
:: The path to where packages will be outputed to
SET PKGDIR=%ROOT%packages
:: The path to the build drop containing the assemblies to be packaged
SET BUILDDROP=\\cpvsbuild\drops\VS\d14rel\raw\25420.01\binaries.x86ret\bin\i386\xmake
:: The build configuration for the reference assemblies
SET CONFIGURATION=Release
:: The icon url to use in the packages
SET ICONURL=https://go.microsoft.com/fwlink/?linkid=825694
:: The license url to use in the packages
SET LICENSEURL=http://go.microsoft.com/fwlink/?LinkId=329770
:: The project url to use in the packages
SET PROJECTURL=http://go.microsoft.com/fwlink/?LinkId=624683

:: Ensure dotnet.exe is on the PATH
where dotnet.exe>NUL 2>&1
IF ERRORLEVEL 1 (
    ECHO Could not find dotnet.exe on the PATH, please open a command window with dotnet.exe on the PATH or install the dotnet CLI first. 1>&2
    EXIT /B 1
)

:: Ensure nuget.exe is on the PATH
where nuget.exe>NUL 2>&1
IF ERRORLEVEL 1 (
    ECHO Could not find nuget.exe on the PATH, please open a command window with nuget.exe on the PATH. 1>&2
    EXIT /B 1
)


:: Create a package for each assembly.  The ordering here sort of matters based on the dependencies of the assemblies
CALL :CreatePackage "Microsoft.Build.Framework"
CALL :CreatePackage "Microsoft.Build"
CALL :CreatePackage "Microsoft.Build.Engine"
CALL :CreatePackage "Microsoft.Build.Utilities.Core"
CALL :CreatePackage "Microsoft.Build.Tasks.Core"
CALL :CreatePackage "Microsoft.Build.Conversion.Core"

GOTO :EOF

:CreatePackage
:: BasePath is the root path of the assembly to build
SET BASEPATH=%ROOT%\%~1
:: The path to the .nuspec
SET NUSPEC=%BASEPATH%\%~1.nuspec
:: The path to the project.json
SET PROJECTJSON=%BASEPATH%\project.json
:: The path to where to copy the shipped assembly locally
SET LIBPATH=%BASEPATH%\lib\net45

IF EXIST "%PROJECTJSON%" (
    :: Restore NuGet packages based on the project.json
    dotnet restore "%PROJECTJSON%"
    IF ERRORLEVEL 1 ECHO Error restoring packages & EXIT /B 1

    :: Build the referece assemblies.  This will build against all frameworks in the project.json
    dotnet build "%PROJECTJSON%" --configuration "%CONFIGURATION%"
    IF ERRORLEVEL 1 EXIT /B 1
)
:: Ensure the packages directory exists otherwise NuGet will complain...
IF NOT EXIST "%PKGDIR%" MKDIR "%PKGDIR%"

:: Copy the lib assembly locally for NuGet to consume
xcopy /ydsi %BUILDDROP%\%~1.dll "%LIBPATH%\"
IF ERRORLEVEL 1 EXIT /B 1

:: Create the NuGet package
NuGet PACK "%NUSPEC%" -BasePath "%BASEPATH%" -Properties "id=%~1;version=%VERSIONMAJOR%.%VERSIONMINOR%.%VERSIONBUILD%;configuration=%CONFIGURATION%;iconUrl=%ICONURL%;licenseUrl=%LICENSEURL%;projectUrl=%PROJECTURL%" -OutputDirectory "%PKGDIR%" -Verbosity Detailed

GOTO :EOF