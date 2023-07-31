// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Sln.Add.Tests
{
    public class GivenDotnetSlnAdd : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Add one or more projects to a solution file.

Usage:
  dotnet sln <SLN_FILE> add [<PROJECT_PATH>...] [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the projects to add to the solution.

Options:
  --in-root                                  Place project in root of the solution, rather than creating a solution folder.
  -s, --solution-folder <solution-folder>    The destination solution folder path to add the projects to.
  -?, -h, --help                             Show command line help";

        public GivenDotnetSlnAdd(ITestOutputHelper log) : base(log)
        {
        }

        private const string ExpectedSlnFileAfterAddingLibProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingLibProjToEmptySln = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingNestedProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""src"", ""src"", ""__SRC_FOLDER_GUID__""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		__LIB_PROJECT_GUID__ = __SRC_FOLDER_GUID__
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithoutMatchingConfigs = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ProjectWithoutMatchingConfigs"", ""ProjectWithoutMatchingConfigs\ProjectWithoutMatchingConfigs.csproj"", ""{C49B64DE-4401-4825-8A88-10DCB5950E57}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		Foo Bar|Any CPU = Foo Bar|Any CPU
		Foo Bar|x64 = Foo Bar|x64
		Foo Bar|x86 = Foo Bar|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|x64.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|x64.Build.0 = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|x86.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Debug|x86.Build.0 = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|Any CPU.Build.0 = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|x64.ActiveCfg = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|x64.Build.0 = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|x86.ActiveCfg = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Release|x86.Build.0 = Release|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|Any CPU.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|Any CPU.Build.0 = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|x64.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|x64.Build.0 = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|x86.ActiveCfg = Debug|Any CPU
		{C49B64DE-4401-4825-8A88-10DCB5950E57}.Foo Bar|x86.Build.0 = Debug|Any CPU
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithMatchingConfigs = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ProjectWithMatchingConfigs"", ""ProjectWithMatchingConfigs\ProjectWithMatchingConfigs.csproj"", ""{C9601CA2-DB64-4FB6-B463-368C7764BF0D}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		Foo Bar|Any CPU = Foo Bar|Any CPU
		Foo Bar|x64 = Foo Bar|x64
		Foo Bar|x86 = Foo Bar|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|x64.ActiveCfg = Debug|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|x64.Build.0 = Debug|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|x86.ActiveCfg = Debug|x86
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Debug|x86.Build.0 = Debug|x86
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|Any CPU.Build.0 = Release|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|x64.ActiveCfg = Release|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|x64.Build.0 = Release|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|x86.ActiveCfg = Release|x86
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Release|x86.Build.0 = Release|x86
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|Any CPU.ActiveCfg = FooBar|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|Any CPU.Build.0 = FooBar|Any CPU
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|x64.ActiveCfg = FooBar|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|x64.Build.0 = FooBar|x64
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|x86.ActiveCfg = FooBar|x86
		{C9601CA2-DB64-4FB6-B463-368C7764BF0D}.Foo Bar|x86.Build.0 = FooBar|x86
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithAdditionalConfigs = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ProjectWithAdditionalConfigs"", ""ProjectWithAdditionalConfigs\ProjectWithAdditionalConfigs.csproj"", ""{A302325B-D680-4C0E-8680-7AE283981624}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		Foo Bar|Any CPU = Foo Bar|Any CPU
		Foo Bar|x64 = Foo Bar|x64
		Foo Bar|x86 = Foo Bar|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|x64.ActiveCfg = Debug|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|x64.Build.0 = Debug|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|x86.ActiveCfg = Debug|x86
		{A302325B-D680-4C0E-8680-7AE283981624}.Debug|x86.Build.0 = Debug|x86
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|Any CPU.Build.0 = Release|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|x64.ActiveCfg = Release|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|x64.Build.0 = Release|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|x86.ActiveCfg = Release|x86
		{A302325B-D680-4C0E-8680-7AE283981624}.Release|x86.Build.0 = Release|x86
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|Any CPU.ActiveCfg = FooBar|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|Any CPU.Build.0 = FooBar|Any CPU
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|x64.ActiveCfg = FooBar|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|x64.Build.0 = FooBar|x64
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|x86.ActiveCfg = FooBar|x86
		{A302325B-D680-4C0E-8680-7AE283981624}.Foo Bar|x86.Build.0 = FooBar|x86
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithInRootOption = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""{84A45D44-B677-492D-A6DA-B3A71135AB8E}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.Build.0 = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.Build.0 = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""TestFolder"", ""TestFolder"", ""__SOLUTION_FOLDER_GUID__""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		__LIB_PROJECT_GUID__ = __SOLUTION_FOLDER_GUID__
	EndGlobalSection
EndGlobal
";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", "add", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln {commandName}".Trim().Split());
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log)
                .Execute("sln", "one.sln", "two.sln", "three.sln", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
        }

        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters")]
        [InlineData("ihaveinv@lidcharacters")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", solutionName, "add", "p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetSlnAdd")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "InvalidSolution.sln", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenInvalidSolutionIsFoundAddPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionPath, LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: "GivenDotnetSlnAdd")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"sln", "App.sln", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryAddPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(@"sln", "add", "App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnAdd")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreated()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingNestedProj);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build", "App.sln");
            cmd.Should().Pass();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreatedBuild(bool fooFirst)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirVS")
                .WithSource()
                .Path;
            string projectToAdd;
            CommandResult cmd;

            if (fooFirst)
            {
                projectToAdd = "foo";
                cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute($"sln", "App.sln", "add", projectToAdd);
                cmd.Should().Pass();
            }

            projectToAdd = Path.Combine("foo", "bar");
            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            if (!fooFirst)
            {
                projectToAdd = "foo";
                cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute($"sln", "App.sln", "add", projectToAdd);
                cmd.Should().Pass();
            }

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build", "App.sln");
            cmd.Should().Pass();

        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles")]
        [InlineData("TestAppWithSlnAnd472CsprojFiles")]
        public void WhenDirectoryContainingProjectIsGivenProjectIsAdded(string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "add", "Lib");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingLibProj);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Fact]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Empty";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "add", directoryToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Multiple";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "add", directoryToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenProjectDirectoryIsAddedSolutionFoldersAreNotCreated()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(0);
            slnFile.Sections.GetSection("NestedProjects").Should().BeNull();
        }

        [Fact]
        public void WhenSharedProjectAddedShouldStillBuild()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Shared", "Shared.shproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();

            cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute();
            cmd.Should().Pass();
        }

        [Theory]
        [InlineData(".")]
        [InlineData("")]
        public void WhenSolutionFolderExistsItDoesNotGetAdded(string firstComponent)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndSolutionFolders", identifier: firstComponent)
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine($"{firstComponent}", "src", "src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            slnFile.Projects.Count().Should().Be(4);

            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(2);

            var solutionFolders = slnFile.Sections.GetSection("NestedProjects").Properties;
            solutionFolders.Count.Should().Be(3);

            solutionFolders["{DDF3765C-59FB-4AA6-BE83-779ED13AA64A}"]
                .Should().Be("{72BFCA87-B033-4721-8712-4D12166B4A39}");

            var newlyAddedSrcFolder = solutionFolderProjects.Single(p => p.Id != "{72BFCA87-B033-4721-8712-4D12166B4A39}");
            solutionFolders[newlyAddedSrcFolder.Id]
                .Should().Be("{72BFCA87-B033-4721-8712-4D12166B4A39}");

            var libProject = slnFile.Projects.Single(p => p.Name == "Lib");
            solutionFolders[libProject.Id]
                .Should().Be(newlyAddedSrcFolder.Id);
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles", ExpectedSlnFileAfterAddingLibProj, "")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles", ExpectedSlnFileAfterAddingLibProj, "{84A45D44-B677-492D-A6DA-B3A71135AB8E}")]
        [InlineData("TestAppWithEmptySln", ExpectedSlnFileAfterAddingLibProjToEmptySln, "")]
        public void WhenValidProjectIsPassedBuildConfigsAreAdded(
            string testAsset,
            string expectedSlnContentsTemplate,
            string expectedProjectGuid)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                expectedSlnContentsTemplate,
                expectedProjectGuid);

            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles")]
        [InlineData("TestAppWithEmptySln")]
        public void WhenValidProjectIsPassedItGetsAdded(string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var projectPath = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectPath));
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenProjectIsAddedSolutionHasUTF8BOM()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithEmptySln")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var preamble = Encoding.UTF8.GetPreamble();
            preamble.Length.Should().Be(3);
            using (var stream = new FileStream(Path.Combine(projectDirectory, "App.sln"), FileMode.Open))
            {
                var bytes = new byte[preamble.Length];
                stream.Read(bytes, 0, bytes.Length);
                bytes.Should().BeEquivalentTo(preamble);
            }
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles")]
        [InlineData("TestAppWithEmptySln")]
        public void WhenInvalidProjectIsPassedItDoesNotGetAdded(string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Library.cs";
            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var expectedNumberOfProjects = slnFile.Projects.Count();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeEmpty();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidProjectWithExceptionMessage, '*', '*'));

            slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            slnFile.Projects.Count().Should().Be(expectedNumberOfProjects);
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles")]
        [InlineData("TestAppWithEmptySln")]
        public void WhenValidProjectIsPassedTheSlnBuilds(string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"sln", "App.sln", "add", "App/App.csproj", "Lib/Lib.csproj");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore", "App.sln")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("build", "App.sln", "--configuration", "Release")
                .Should().Pass();

            var reasonString = "should be built in release mode, otherwise it means build configurations are missing from the sln file";

            var appPathCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "App", "App.csproj"));
            new DirectoryInfo(appPathCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);
            new DirectoryInfo(appPathCalculator.GetOutputDirectory(configuration: "Release")).Should().Exist()
                .And.HaveFile("App.dll");

            var libPathCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "Lib", "Lib.csproj"));
            new DirectoryInfo(libPathCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);
            new DirectoryInfo(libPathCalculator.GetOutputDirectory(configuration: "Release")).Should().Exist()
                .And.HaveFile("Lib.dll");
        }

        [Theory]
        [InlineData("TestAppWithSlnAndExistingCsprojReferences")]
        [InlineData("TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep")]
        public void WhenSolutionAlreadyContainsProjectItDoesntDuplicate(string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.SolutionAlreadyContainsProject, solutionPath, projectToAdd));
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenPassedMultipleProjectsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd, "idonotexist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, "idonotexist.csproj"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/522")]
        public void WhenPassedAnUnknownProjectTypeItFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProject")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("UnknownProject", "UnknownProject.unknownproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo("Unsupported project type. Check with your sdk provider.");

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("SlnFileWithNoProjectReferencesAndCSharpProject", "CSharpProject", "CSharpProject.csproj", ProjectTypeGuids.CSharpProjectTypeGuid)]
        [InlineData("SlnFileWithNoProjectReferencesAndFSharpProject", "FSharpProject", "FSharpProject.fsproj", ProjectTypeGuids.FSharpProjectTypeGuid)]
        [InlineData("SlnFileWithNoProjectReferencesAndVBProject", "VBProject", "VBProject.vbproj", ProjectTypeGuids.VBProjectTypeGuid)]
        [InlineData("SlnFileWithNoProjectReferencesAndUnknownProjectWithSingleProjectTypeGuid", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        [InlineData("SlnFileWithNoProjectReferencesAndUnknownProjectWithMultipleProjectTypeGuids", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        public void WhenPassedAProjectItAddsCorrectProjectTypeGuid(
            string testAsset,
            string projectDir,
            string projectName,
            string expectedTypeGuid)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine(projectDir, projectName);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var nonSolutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid);
            nonSolutionFolderProjects.Count().Should().Be(1);
            nonSolutionFolderProjects.Single().TypeGuid.Should().Be(expectedTypeGuid);
        }

        [Fact]
        public void WhenPassedAProjectWithoutATypeGuidItErrors()
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProjectType")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(solutionDirectory, "App.sln");
            var contentBefore = File.ReadAllText(solutionPath);

            var projectToAdd = Path.Combine("UnknownProject", "UnknownProject.unknownproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute($"sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.UnsupportedProjectType,
                    Path.Combine(solutionDirectory, projectToAdd)));
            cmd.StdOut.Should().BeEmpty();

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [Fact]
        private void WhenSlnContainsSolutionFolderWithDifferentCasingItDoesNotCreateDuplicate()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCaseSensitiveSolutionFolders")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(1);
        }

        [Fact]
        public void WhenProjectWithoutMatchingConfigurationsIsAddedSolutionMapsToFirstAvailable()
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, "App.sln");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute($"sln", "add", "ProjectWithoutMatchingConfigs");
            result.Should().Pass();

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnFileAfterAddingProjectWithoutMatchingConfigs);
        }

        [Fact]
        public void WhenProjectWithMatchingConfigurationsIsAddedSolutionMapsAll()
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, "App.sln");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute($"sln", "add", "ProjectWithMatchingConfigs");
            result.Should().Pass();

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnFileAfterAddingProjectWithMatchingConfigs);
        }

        [Fact]
        public void WhenProjectWithAdditionalConfigurationsIsAddedSolutionDoesNotMapThem()
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, "App.sln");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute($"sln", "add", "ProjectWithAdditionalConfigs");
            result.Should().Pass();

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnFileAfterAddingProjectWithAdditionalConfigs);
        }

        [Fact]
        public void ItAddsACSharpProjectThatIsMultitargeted()
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("MultitargetedCS", "MultitargetedCS.csproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute($"sln", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Fact]
        public void ItAddsAVisualBasicProjectThatIsMultitargeted()
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("MultitargetedVB", "MultitargetedVB.vbproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute($"sln", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Fact]
        public void ItAddsAnFSharpProjectThatIsMultitargeted()
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(solutionDirectory, "App.sln");
            var projectToAdd = Path.Combine("MultitargetedFS", "MultitargetedFS.fsproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute($"sln", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Fact]
        public void WhenNestedProjectIsAddedAndInRootOptionIsPassedNoSolutionFoldersAreCreated()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", "--in-root", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithInRootOption);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Fact]
        public void WhenSolutionFolderIsPassedProjectsAreAddedThere()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", "--solution-folder", "TestFolder", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Fact]
        public void WhenSolutionFolderAndInRootIsPassedItFails()
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(solutionDirectory, "App.sln");
            var contentBefore = File.ReadAllText(solutionPath);

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute($"sln", "App.sln", "add", "--solution-folder", "blah", "--in-root", projectToAdd);
            cmd.Should().Fail();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            cmd.StdErr.Should().Be(Microsoft.DotNet.Tools.Sln.LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("/TestFolder//", "ForwardSlash")]
        [InlineData("\\TestFolder\\\\", "BackwardSlash")]
        public void WhenSolutionFolderIsPassedWithDirectorySeparatorFolderStructureIsCorrect(string solutionFolder, string testIdentifier)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: testIdentifier)
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "add", "--solution-folder", solutionFolder, projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        private string GetExpectedSlnContents(
            string slnPath,
            string slnTemplate,
            string expectedLibProjectGuid = null)
        {
            var slnFile = SlnFile.Read(slnPath);

            if (string.IsNullOrEmpty(expectedLibProjectGuid))
            {
                var matchingProjects = slnFile.Projects
                    .Where((p) => p.FilePath.EndsWith("Lib.csproj"))
                    .ToList();

                matchingProjects.Count.Should().Be(1);
                var slnProject = matchingProjects[0];
                expectedLibProjectGuid = slnProject.Id;
            }
            var slnContents = slnTemplate.Replace("__LIB_PROJECT_GUID__", expectedLibProjectGuid);

            var matchingSrcFolder = slnFile.Projects
                    .Where((p) => p.FilePath == "src")
                    .ToList();
            if (matchingSrcFolder.Count == 1)
            {
                slnContents = slnContents.Replace("__SRC_FOLDER_GUID__", matchingSrcFolder[0].Id);
            }

            var matchingSolutionFolder = slnFile.Projects
                    .Where((p) => p.FilePath == "TestFolder")
                    .ToList();
            if (matchingSolutionFolder.Count == 1)
            {
                slnContents = slnContents.Replace("__SOLUTION_FOLDER_GUID__", matchingSolutionFolder[0].Id);
            }

            return slnContents;
        }

        [Fact]
        public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage()
        {
            VerifySuggestionAndUsage("");
        }

        [Fact]
        public void WhenSolutionIsPassedAsProjectWithInRootItPrintsSuggestionAndUsage()
        {
            VerifySuggestionAndUsage("--in-root");
        }

        [Fact]
        public void WhenSolutionIsPassedAsProjectWithSolutionFolderItPrintsSuggestionAndUsage()
        {
            VerifySuggestionAndUsage("--solution-folder");
        }
        private void VerifySuggestionAndUsage(string arguments)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var projectArg = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "add", arguments, "Lib", "App.sln", projectArg);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo(
                string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, "App.sln") + Environment.NewLine
                + CommonLocalizableStrings.DidYouMean + Environment.NewLine
                + $"  dotnet sln App.sln add {arguments} Lib {projectArg}"
            );
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
    }
}
