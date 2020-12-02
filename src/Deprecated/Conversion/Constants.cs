// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Conversion
{
    /// <summary>
    /// Contains strings identifying hint paths that we should remove
    /// </summary>
    /// <owner>AJenner</owner>
    static internal class LegacyFrameworkPaths
    {
        internal const string RTMFrameworkPath       = "MICROSOFT.NET\\FRAMEWORK\\V1.0.3705";
        internal const string EverettFrameworkPath   = "MICROSOFT.NET\\FRAMEWORK\\V1.1.4322";
        internal const string JSharpRTMFrameworkPath = "MICROSOFT VISUAL JSHARP .NET\\FRAMEWORK\\V1.0.4205";
    }

    /// <summary>
    /// Contains the names of the known elements in the VS.NET project file.
    /// </summary>
    /// <owner>RGoel</owner>
    static internal class VSProjectElements
    {
        internal const string visualStudioProject = "VisualStudioProject";
        internal const string visualJSharp        = "VISUALJSHARP";
        internal const string cSharp              = "CSHARP";
        internal const string visualBasic         = "VisualBasic";
        internal const string ECSharp             = "ECSHARP";
        internal const string EVisualBasic        = "EVisualBasic";
        internal const string build               = "Build";
        internal const string settings            = "Settings";
        internal const string config              = "Config";
        internal const string platform            = "Platform";
        internal const string interopRegistration = "InteropRegistration";
        internal const string references          = "References";
        internal const string reference           = "Reference";
        internal const string files               = "Files";
        internal const string imports             = "Imports";
        internal const string import              = "Import";
        internal const string include             = "Include";
        internal const string exclude             = "Exclude";
        internal const string file                = "File";
        internal const string folder              = "Folder";
        internal const string startupServices     = "StartupServices";
        internal const string service             = "Service";
        internal const string userProperties      = "UserProperties";
        internal const string otherProjectSettings= "OtherProjectSettings";
        internal const string PocketPC            = "Pocket PC";
        internal const string WindowsCE           = "Windows CE";
        internal const string Smartphone          = "Smartphone";
        internal const string SystemDataCommon    = "System.Data.Common";
        internal const string SystemSR            = "System.SR";
        internal const string MSCorLib            = "MSCorLib";
    }

    /// <summary>
    /// Contains the names of the known elements in the VS.NET project file.
    /// </summary>
    /// <owner>RGoel</owner>
    static internal class VSProjectAttributes
    {
        internal const string relPath             = "RelPath";
        internal const string name                = "Name";
        internal const string guid                = "Guid";
        internal const string project             = "Project";
        internal const string projectType         = "ProjectType";
        internal const string local               = "Local";
        internal const string assemblyName        = "AssemblyName";
        internal const string importNamespace     = "Namespace";
        internal const string id                  = "ID";
        internal const string link                = "Link";
        internal const string buildAction         = "BuildAction";
        internal const string buildActionNone     = "None";
        internal const string buildActionResource = "EmbeddedResource";
        internal const string webReferences       = "WebReferences";
        internal const string webReferenceUrl     = "WebReferenceUrl";
        internal const string projectGuid         = "ProjectGuid";
        internal const string preBuildEvent       = "PreBuildEvent";
        internal const string postBuildEvent      = "PostBuildEvent";
        internal const string productVersion      = "ProductVersion";
        internal const string schemaVersion       = "SchemaVersion";
        internal const string outputPath          = "OutputPath";
        internal const string officeDocumentPath  = "OfficeDocumentPath";
        internal const string officeDocumentType  = "OfficeProjectType";
        internal const string officeProject       = "OfficeProject";
        internal const string additionalOptions   = "AdditionalOptions";
        internal const string platform            = "Platform";
        internal const string selectedDevice      = "SelectedDevice";
        internal const string deploymentPlatform  = "DeploymentPlatform";
        internal const string incrementalBuild    = "IncrementalBuild";
        internal const string hintPath            = "HintPath";
        internal const string documentationFile   = "DocumentationFile";
        internal const string debugType           = "DebugType";
        internal const string debugTypeNone       = "none";
        internal const string debugTypeFull       = "full";
        internal const string errorReport         = "ErrorReport";
        internal const string errorReportPrompt   = "prompt";
    }

    /// <summary>
    /// Contains the names of some of the hard-coded strings we'll be inserting into the newly converted MSBuild project file.
    /// </summary>
    /// <owner>RGoel</owner>
    static internal class XMakeProjectStrings
    {
        internal const string project                     = "Project";
        internal const string defaultTargets              = "Build";
        internal const string msbuildVersion              = "MSBuildVersion";
        internal const string xmlns                       = "xmlns";
        internal const string importPrefix                = "$(MSBuildToolsPath)\\";
        internal const string importSuffix                = ".targets";
        internal const string targetsFilenamePrefix       = "Microsoft.";
        internal const string csharpTargets               = "CSharp";
        internal const string visualBasicTargets          = "VisualBasic";
        internal const string visualJSharpTargets         = "VisualJSharp";
        internal const string triumphImport               = "$(MSBuildExtensionsPath)\\Microsoft\\VisualStudio\\v9.0\\OfficeTools\\Microsoft.VisualStudio.OfficeTools.targets";
        internal const string officeTargetsVS2005Import   = @"$(MSBuildExtensionsPath)\Microsoft.VisualStudio.OfficeTools.targets";
        internal const string officeTargetsVS2005Import2  = @"$(MSBuildExtensionsPath)\Microsoft.VisualStudio.OfficeTools2.targets";
        internal const string officeTargetsVS2005Repair   = @"OfficeTools\Microsoft.VisualStudio.Tools.Office.targets";
        internal const string configurationPrefix         = " '$(Configuration)' == '";
        internal const string configurationSuffix         = "' ";
        internal const string configuration               = "Configuration";
        internal const string platformPrefix              = " '$(Platform)' == '";
        internal const string platformSuffix              = "' ";
        internal const string platform                    = "Platform";
        internal const string configplatformPrefix        = " '$(Configuration)|$(Platform)' == '";
        internal const string configplatformSeparator     = "|";
        internal const string configplatformSuffix        = "' ";
        internal const string defaultConfiguration        = "Debug";
        internal const string defaultPlatform             = "AnyCPU";
        internal const string x86Platform                 = "x86";
        internal const string debugSymbols                = "DebugSymbols";
        internal const string reference                   = "Reference";
        internal const string comReference                = "COMReference";
        internal const string projectReference            = "ProjectReference";
        internal const string import                      = "Import";
        internal const string service                     = "Service";
        internal const string folder                      = "Folder";
        internal const string link                        = "Link";
        internal const string autogen                     = "AutoGen";
        internal const string webReferences               = "WebReferences";
        internal const string webReferenceUrl             = "WebReferenceUrl";
        internal const string relPath                     = "RelPath";
        internal const string visualStudio                = "VisualStudio";
        internal const string webRefEnableProperties      = "WebReference_EnableProperties";
        internal const string webRefEnableSqlTypes        = "WebReference_EnableSQLTypes";
        internal const string webRefEnableLegacyEventing  = "WebReference_EnableLegacyEventingModel";
        internal const string xmlNamespace                = "http://schemas.microsoft.com/developer/msbuild/2003";

        internal const string cSharpGuid                  = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
        internal const string visualBasicGuid             = "F184B08F-C81C-45F6-A57F-5ABD9991F28F";
        internal const string visualJSharpGuid            = "E6FDF86B-F3D1-11D4-8576-0002A516ECE8";
        internal const string triumphProjectTypeGuid      = "BAA0C2D2-18E2-41B9-852F-F413020CAA33";
        internal const string VSDCSProjectTypeGuid        = "4D628B5B-2FBC-4AA6-8C16-197242AEB884";
        internal const string VSDVBProjectTypeGuid        = "68B1623D-7FB9-47D8-8664-7ECEA3297D4F";
        internal const string wpfFlavorGuid               = "60dc8134-eba5-43b8-bcc9-bb4bc16c2548";
        internal const string projectTypeGuids            = "ProjectTypeGuids";
        internal const string platformID                  = "PlatformID";
        internal const string platformFamilyName          = "PlatformFamilyName";
        internal const string deployTargetSuffix          = "DeployDirSuffix";
        internal const string disableCSHostProc           = "<FlavorProperties GUID=\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\">\n<HostingProcess disable=\"1\" />\n</FlavorProperties>";
        internal const string disableVBHostProc           = "<FlavorProperties GUID=\"{F184B08F-C81C-45F6-A57F-5ABD9991F28F}\">\n<HostingProcess disable=\"1\" />\n</FlavorProperties>";
        internal const string SDECSTargets                = "Microsoft.CompactFramework.CSharp.targets";
        internal const string SDEVBTargets                = "Microsoft.CompactFramework.VisualBasic.targets";
        internal const string TargetFrameworkVersion      = "TargetFrameworkVersion";
        internal const string TargetFrameworkSubset       = "TargetFrameworkSubset";
        internal const string TargetFrameworkProfile      = "TargetFrameworkProfile";
        internal const string ClientProfile               = "Client";
        internal const string vOne                        = "v1.0";
        internal const string vTwo                        = "v2.0";
        internal const string noWarn                      = "NoWarn";
        internal const string disabledVBWarnings          = "42016,42017,42018,42019,42032,42353,42354,42355";
        internal const string xmlFileExtension            = ".xml";
        internal const string csdprojFileExtension        = ".csdproj";
        internal const string vbdprojFileExtension        = ".vbdproj";
        internal const string csprojFileExtension         = ".csproj";
        internal const string vbprojFileExtension         = ".vbproj";
        internal const string myType                      = "MyType";
        internal const string web                         = "Web";
        internal const string windowsFormsWithCustomSubMain = "WindowsFormsWithCustomSubMain";
        internal const string windows                     = "Windows";
        internal const string codeAnalysisRuleAssemblies  = "CodeAnalysisRuleAssemblies";
        internal const string console                     = "Console";
        internal const string empty                       = "Empty";
        internal const string exe                         = "Exe";
        internal const string library                     = "Library";
        internal const string winExe                      = "WinExe";
        internal const string outputType                  = "OutputType";
        internal const string fileUpgradeFlags            = "FileUpgradeFlags";
        internal const string content                     = "Content";
        internal const string copytooutput                = "CopyToOutputDirectory";
        internal const string preservenewest              = "PreserveNewest";
        internal const string toolsVersion                = MSBuildConstants.CurrentToolsVersion;
        internal const string vbTargetsVS2008             = @"$(MSBuildToolsPath)\Microsoft.VisualBasic.targets";
        internal const string vbTargetsVS2005             = @"$(MSBuildBinPath)\Microsoft.VisualBasic.targets";
        internal const string vsToolsPath                 = @"VSToolsPath";
        internal const string visualStudioVersion         = @"VisualStudioVersion";
        internal const string toRepairPatternForAssetCompat = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\";
        internal const string toRepairPatternForAssetCompatBeforeV10 = @"$(MSBuildExtensionsPath)\Microsoft\VisualStudio\";
        internal const string toRepairPatternForAssetCompatV10 = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\";
        internal const string repairHardCodedPathPattern = @"^v\d{1,2}\.\d\\";
    }
}
