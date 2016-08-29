// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class VCProjectParser_Tests
    {
        public void TestGetReferencedProjectGuids()
        {
            string vcProjectContents =
            #region FullVCProjectFile
                @"<?xml version='1.0' encoding='Windows-1252'?>
                <VisualStudioProject
	                ProjectType='Visual C++'
	                Version='8.00'
	                Name='MainApp'
	                ProjectGUID='{1263E1EC-59F4-4E1B-A017-04620BA3F132}'
	                RootNamespace='MainApp'
	                Keyword='ManagedCProj'
	                SignManifests='true'
	                >
	                <Platforms>
		                <Platform
			                Name='Win32'
		                />
	                </Platforms>
	                <ToolFiles>
	                </ToolFiles>
	                <Configurations>
		                <Configuration
			                Name='Debug|Win32'
			                OutputDirectory='$(SolutionDir)$(ConfigurationName)'
			                IntermediateDirectory='$(ConfigurationName)'
			                ConfigurationType='1'
			                CharacterSet='2'
			                ManagedExtensions='1'
			                >
			                <Tool
				                Name='VCPreBuildEventTool'
			                />
			                <Tool
				                Name='VCCustomBuildTool'
			                />
			                <Tool
				                Name='VCXMLDataGeneratorTool'
			                />
			                <Tool
				                Name='VCWebServiceProxyGeneratorTool'
			                />
			                <Tool
				                Name='VCMIDLTool'
			                />
			                <Tool
				                Name='VCCLCompilerTool'
				                Optimization='0'
				                PreprocessorDefinitions='WIN32;_DEBUG'
				                MinimalRebuild='false'
				                BasicRuntimeChecks='0'
				                RuntimeLibrary='3'
				                UsePrecompiledHeader='2'
				                WarningLevel='3'
				                DebugInformationFormat='3'
			                />
			                <Tool
				                Name='VCManagedResourceCompilerTool'
			                />
			                <Tool
				                Name='VCResourceCompilerTool'
			                />
			                <Tool
				                Name='VCPreLinkEventTool'
			                />
			                <Tool
				                Name='VCLinkerTool'
				                AdditionalDependencies='$(NoInherit)'
				                LinkIncremental='2'
				                GenerateDebugInformation='true'
				                AssemblyDebug='1'
				                TargetMachine='1'
			                />
			                <Tool
				                Name='VCALinkTool'
			                />
			                <Tool
				                Name='VCManifestTool'
			                />
			                <Tool
				                Name='VCXDCMakeTool'
			                />
			                <Tool
				                Name='VCBscMakeTool'
			                />
			                <Tool
				                Name='VCFxCopTool'
			                />
			                <Tool
				                Name='VCAppVerifierTool'
			                />
			                <Tool
				                Name='VCWebDeploymentTool'
			                />
			                <Tool
				                Name='VCPostBuildEventTool'
			                />
		                </Configuration>
		                <Configuration
			                Name='Release|Win32'
			                OutputDirectory='$(SolutionDir)$(ConfigurationName)'
			                IntermediateDirectory='$(ConfigurationName)'
			                ConfigurationType='1'
			                CharacterSet='2'
			                ManagedExtensions='1'
			                >
			                <Tool
				                Name='VCPreBuildEventTool'
			                />
			                <Tool
				                Name='VCCustomBuildTool'
			                />
			                <Tool
				                Name='VCXMLDataGeneratorTool'
			                />
			                <Tool
				                Name='VCWebServiceProxyGeneratorTool'
			                />
			                <Tool
				                Name='VCMIDLTool'
			                />
			                <Tool
				                Name='VCCLCompilerTool'
				                Optimization='2'
				                PreprocessorDefinitions='WIN32;NDEBUG'
				                MinimalRebuild='false'
				                RuntimeLibrary='2'
				                UsePrecompiledHeader='2'
				                WarningLevel='3'
				                DebugInformationFormat='3'
			                />
			                <Tool
				                Name='VCManagedResourceCompilerTool'
			                />
			                <Tool
				                Name='VCResourceCompilerTool'
			                />
			                <Tool
				                Name='VCPreLinkEventTool'
			                />
			                <Tool
				                Name='VCLinkerTool'
				                AdditionalDependencies='$(NoInherit)'
				                LinkIncremental='1'
				                GenerateDebugInformation='true'
				                TargetMachine='1'
			                />
			                <Tool
				                Name='VCALinkTool'
			                />
			                <Tool
				                Name='VCManifestTool'
			                />
			                <Tool
				                Name='VCXDCMakeTool'
			                />
			                <Tool
				                Name='VCBscMakeTool'
			                />
			                <Tool
				                Name='VCFxCopTool'
			                />
			                <Tool
				                Name='VCAppVerifierTool'
			                />
			                <Tool
				                Name='VCWebDeploymentTool'
			                />
			                <Tool
				                Name='VCPostBuildEventTool'
			                />
		                </Configuration>
	                </Configurations>
	                <References>
		                <AssemblyReference
			                RelativePath='System.dll'
			                AssemblyName='System, Version=2.0.0.0, PublicKeyToken=b77a5c561934e089, processorArchitecture=MSIL'
		                />
		                <AssemblyReference
			                RelativePath='System.Data.dll'
			                AssemblyName='System.Data, Version=2.0.0.0, PublicKeyToken=b77a5c561934e089, processorArchitecture=x86'
		                />
		                <AssemblyReference
			                RelativePath='System.Xml.dll'
			                AssemblyName='System.Xml, Version=2.0.0.0, PublicKeyToken=b77a5c561934e089, processorArchitecture=MSIL'
		                />
		                <ProjectReference
			                ReferencedProjectIdentifier='{C19D89DA-997C-46D7-8569-0F6B14D5EE58}'
			                RelativePathToProject='.\ClassLibrary1\ClassLibrary1.csproj'
		                />
		                <ProjectReference
			                ReferencedProjectIdentifier='{95EDBFFD-540C-42C2-BE2C-F026B9169744}'
			                RelativePathToProject='.\ClassLibrary2\ClassLibrary2.csproj'
		                />
		                <ProjectReference
			                ReferencedProjectIdentifier='{2B050DEB-56B6-463E-BC33-581BC33B3BD8}'
			                RelativePathToProject='.\ClassLibrary3\ClassLibrary3.csproj'
		                />
	                </References>
	                <Files>
		                <Filter
			                Name='Source Files'
			                Filter='cpp;c;cc;cxx;def;odl;idl;hpj;bat;asm;asmx'
			                UniqueIdentifier='{4FC737F1-C7A5-4376-A066-2A32D752A2FF}'
			                >
			                <File
				                RelativePath='.\AssemblyInfo.cpp'
				                >
			                </File>
			                <File
				                RelativePath='.\MainApp.cpp'
				                >
			                </File>
			                <File
				                RelativePath='.\stdafx.cpp'
				                >
				                <FileConfiguration
					                Name='Debug|Win32'
					                >
					                <Tool
						                Name='VCCLCompilerTool'
						                UsePrecompiledHeader='1'
					                />
				                </FileConfiguration>
				                <FileConfiguration
					                Name='Release|Win32'
					                >
					                <Tool
						                Name='VCCLCompilerTool'
						                UsePrecompiledHeader='1'
					                />
				                </FileConfiguration>
			                </File>
		                </Filter>
		                <Filter
			                Name='Header Files'
			                Filter='h;hpp;hxx;hm;inl;inc;xsd'
			                UniqueIdentifier='{93995380-89BD-4b04-88EB-625FBE52EBFB}'
			                >
			                <File
				                RelativePath='.\resource.h'
				                >
			                </File>
			                <File
				                RelativePath='.\stdafx.h'
				                >
			                </File>
		                </Filter>
		                <Filter
			                Name='Resource Files'
			                Filter='rc;ico;cur;bmp;dlg;rc2;rct;bin;rgs;gif;jpg;jpeg;jpe;resx;tiff;tif;png;wav'
			                UniqueIdentifier='{67DA6AB6-F800-4c08-8B7A-83BB121AAD01}'
			                >
			                <File
				                RelativePath='.\app.ico'
				                >
			                </File>
			                <File
				                RelativePath='.\app.rc'
				                >
			                </File>
		                </Filter>
		                <File
			                RelativePath='.\ReadMe.txt'
			                >
		                </File>
	                </Files>
	                <Globals>
	                </Globals>
                </VisualStudioProject>";
            #endregion

            string realVcProjectContents = vcProjectContents.Replace('\'', '"');
            string tempPath = Path.GetTempFileName();

            File.WriteAllText(tempPath, realVcProjectContents);

            try
            {
                List<string> referencedProjectGuids = VCProjectParser.GetReferencedProjectGuids(tempPath);
                Assertion.AssertEquals(3, referencedProjectGuids.Count);
                Assertion.Assert(referencedProjectGuids.Contains("{C19D89DA-997C-46D7-8569-0F6B14D5EE58}"));
                Assertion.Assert(referencedProjectGuids.Contains("{95EDBFFD-540C-42C2-BE2C-F026B9169744}"));
                Assertion.Assert(referencedProjectGuids.Contains("{2B050DEB-56B6-463E-BC33-581BC33B3BD8}"));
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
