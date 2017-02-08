// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Xml;

using Microsoft.Build.Conversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ProjectRootElement = Microsoft.Build.Construction.ProjectRootElement;

namespace Microsoft.Build.UnitTests
{
    internal static partial class Helpers
    {
        /// <summary>
        /// Converts a VS7/Everett project to Whidbey format, and compares the result with the expected
        /// contents to make sure they match.
        /// </summary>
        /// <param name="everettProjectContents"></param>
        /// <param name="whidbeyProjectContents"></param>
        /// <owner>RGoel</owner>
        internal static void ConvertAndCompare
            (
            string sourceProjectContents,
            string targetProjectContents
            )
        {
            ConvertAndCompare(sourceProjectContents, targetProjectContents, null);
        }

        /// <summary>
        /// Converts a VS7/Everett project to Whidbey format, and compares the result with the expected
        /// contents to make sure they match.
        /// </summary>
        /// <param name="everettProjectContents"></param>
        /// <param name="whidbeyProjectContents"></param>
        /// <param name="additionalDummyFileToCreateInEverettProjectDirectory"></param>
        /// <param name="isMinorUpgrade"></param>
        /// <owner>RGoel</owner>
        internal static void ConvertAndCompare
            (
            string sourceProjectContents,
            string targetProjectContents,
            string additionalDummyFileToCreateInEverettProjectDirectory,
            bool isMinorUpgrade = false
            )
        {
            string sourceProjectFile = ObjectModelHelpers.CreateTempFileOnDiskNoFormat(sourceProjectContents);

            string dummyExtraFile = null;
            if (additionalDummyFileToCreateInEverettProjectDirectory != null)
            {
                dummyExtraFile = Path.Combine(Path.GetDirectoryName(sourceProjectFile), additionalDummyFileToCreateInEverettProjectDirectory);

                File.WriteAllText(dummyExtraFile, String.Empty);
            }

            try
            {
                ProjectFileConverter converter = new ProjectFileConverter();

                converter.OldProjectFile = sourceProjectFile;
                converter.IsUserFile = false;
                converter.IsMinorUpgrade = isMinorUpgrade;
                ProjectRootElement project = converter.ConvertInMemory();

                Helpers.CompareProjectXml(targetProjectContents, project.RawXml);
            }
            finally
            {
                File.Delete(sourceProjectFile);

                if (dummyExtraFile != null)
                {
                    File.Delete(dummyExtraFile);
                }
            }
        }
    }

    /***************************************************************************
     * 
     * Class:        ProjectFileConverter_Tests
     * Owner:       jomof
     * 
     * This class contains the unit tests for the " ProjectFileConverter" class.  
     * See the comments in that class for a description of its purpose.
     * 
     **************************************************************************/
    [TestClass]
    public class  ProjectFileConverter_Tests
    {
        /***********************************************************************
         *
         * Method:   ProjectFileConverter_Tests.MakeRelativeWithHash
         * Owner:    jomof
         * 
         * Test where paths with '#' in them can be converted into relative 
         * paths
         * 
         **********************************************************************/
        [TestMethod]
        public void MakeRelativeWithHash()
        {
            // Intentionally making the paths not the same case, because this should be irrelevant.            
            string path1 = @"D:\Public\Samples\Visual J# .NET 2003\Crosslanguage\Copy (8) of TilePuzzle\TileDriver\TileDriver.vcproj";
            string path2 = @"D:\public\Samples\Visual J# .NET 2003\Crosslanguage\Copy (8) of TilePuzzle\PUZZLE.vjsproj";

            string rel = ProjectFileConverter.RelativePathTo(path2, path1);
            Console.WriteLine(rel);

            Assert.AreEqual(@"TileDriver\TileDriver.vcproj", rel);
        }
        
        /***********************************************************************
         *
         * Method:   ProjectFileConverter_Tests.MakeRelativeWithSpace
         * Owner:    RGoel
         * 
         * Test where paths where the relative path ends up having a <space> in
         * it.
         * 
         **********************************************************************/
        [TestMethod]
        public void MakeRelativeWithSpace()
        {
            string path1 = @"D:\Public\Samples\Tile Driver\Tile#Driver.vcproj";
            string path2 = @"D:\public\Samples\Main App\Main App.vcproj";

            string rel = ProjectFileConverter.RelativePathTo(path2, path1);
            Console.WriteLine(rel);

            Assert.AreEqual(@"..\Tile Driver\Tile#Driver.vcproj", rel);
        }

        /// <summary>
        /// Per this bug, any references to resx files that were empty
        /// should be removed from the project. The files should not be
        /// deleted though.
        /// </summary>
        [TestMethod]
        public void RemoveReferencesToEmptyResxPerBug248965()
        {
            string temp = Path.GetTempPath();
            string projFolder = temp + "\\projfolder";
            Directory.CreateDirectory(projFolder);
            string projectFile = projFolder + "\\project.proj";
            string resxFile1 = projFolder + "\\emptyresx1.resx";
            string resxFile2 = projFolder + "\\resx2.resx";
            string resxFile3 = projFolder + "\\emptyresx3.resx";
            string resxFile4 = projFolder + "\\resx4.resx";
            string resxFile5 = temp + "\\emptyresx5.resx";
            string resxFile6 = temp + "\\resx6.resx";
            string newProjectFile = "";

            #region projectContent
            string projectContent =
                @"<VisualStudioProject>
                    <CSHARP
                        ProjectType = ""Local""
                        ProductVersion = ""7.10.3077""
                        SchemaVersion = ""2.0""
                        ProjectGuid = ""{C8C8D42E-57AA-4911-BE3A-0C04D7497B6B}""
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = ""App.ico""
                                AssemblyKeyContainerName = """"
                                AssemblyName = ""testEmptyResx""
                                AssemblyOriginatorKeyFile = """"
                                DefaultClientScript = ""JScript""
                                DefaultHTMLPageLayout = ""Grid""
                                DefaultTargetSchema = ""IE50""
                                DelaySign = ""false""
                                OutputType = ""WinExe""
                                PreBuildEvent = """"
                                PostBuildEvent = """"
                                RootNamespace = ""testEmptyResx""
                                RunPostBuildEvent = ""OnBuildSuccess""
                                StartupObject = """"
                            >
                                <Config
                                    Name = ""Debug""
                                    AllowUnsafeBlocks = ""false""
                                    BaseAddress = ""285212672""
                                    CheckForOverflowUnderflow = ""false""
                                    ConfigurationOverrideFile = """"
                                    DefineConstants = ""DEBUG;TRACE""
                                    DocumentationFile = """"
                                    DebugSymbols = ""true""
                                    FileAlignment = ""4096""
                                    IncrementalBuild = ""false""
                                    NoStdLib = ""false""
                                    NoWarn = """"
                                    Optimize = ""false""
                                    OutputPath = ""bin\Debug\""
                                    RegisterForComInterop = ""false""
                                    RemoveIntegerChecks = ""false""
                                    TreatWarningsAsErrors = ""false""
                                    WarningLevel = ""4""
                                />
                                <Config
                                    Name = ""Release""
                                    AllowUnsafeBlocks = ""false""
                                    BaseAddress = ""285212672""
                                    CheckForOverflowUnderflow = ""false""
                                    ConfigurationOverrideFile = """"
                                    DefineConstants = ""TRACE""
                                    DocumentationFile = """"
                                    DebugSymbols = ""false""
                                    FileAlignment = ""4096""
                                    IncrementalBuild = ""false""
                                    NoStdLib = ""false""
                                    NoWarn = """"
                                    Optimize = ""true""
                                    OutputPath = ""bin\Release\""
                                    RegisterForComInterop = ""false""
                                    RemoveIntegerChecks = ""false""
                                    TreatWarningsAsErrors = ""false""
                                    WarningLevel = ""4""
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = ""System""
                                    AssemblyName = ""System""
                                    HintPath = ""..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll""
                                />
                                <Reference
                                    Name = ""System.Data""
                                    AssemblyName = ""System.Data""
                                    HintPath = ""..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Data.dll""
                                />
                                <Reference
                                    Name = ""System.Drawing""
                                    AssemblyName = ""System.Drawing""
                                    HintPath = ""..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Drawing.dll""
                                />
                                <Reference
                                    Name = ""System.Windows.Forms""
                                    AssemblyName = ""System.Windows.Forms""
                                    HintPath = ""..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Windows.Forms.dll""
                                />
                                <Reference
                                    Name = ""System.XML""
                                    AssemblyName = ""System.XML""
                                    HintPath = ""..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll""
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = ""App.ico""
                                    BuildAction = ""Content""
                                />
                                <File
                                    RelPath = ""AssemblyInfo.cs""
                                    BuildAction = ""Compile""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile1) + @"""
                                    BuildAction = ""EmbeddedResource""
                                />
                                <File
                                    RelPath = """ + Path.GetFileNameWithoutExtension(resxFile2) + @".cs""
                                    SubType = ""Form""
                                    BuildAction = ""Compile""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile5) + @"""
                                    Link = ""..\" + Path.GetFileName(resxFile5) + @"""
                                    BuildAction = ""EmbeddedResource""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile6) + @"""
                                    Link = ""..\" + Path.GetFileName(resxFile6) + @"""
                                    BuildAction = ""EmbeddedResource""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile2) + @"""
                                    DependentUpon = """ + Path.GetFileNameWithoutExtension(resxFile2) + @".cs""
                                    BuildAction = ""EmbeddedResource""
                                />
                                <File
                                    RelPath = """ + Path.GetFileNameWithoutExtension(resxFile3) + @".cs""
                                    SubType = ""Form""
                                    BuildAction = ""Compile""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile3) + @"""
                                    DependentUpon = """ + Path.GetFileNameWithoutExtension(resxFile3) + @".cs""
                                    BuildAction = ""EmbeddedResource""
                                />
                                <File
                                    RelPath = """ + Path.GetFileName(resxFile4) + @"""
                                    BuildAction = ""EmbeddedResource""
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ";
            #endregion

            try
            {
                using (StreamWriter sw = new StreamWriter(projectFile))
                    sw.Write(projectContent);
                using (StreamWriter sw = new StreamWriter(resxFile1))
                    sw.Write("");
                using (StreamWriter sw = new StreamWriter(resxFile2))
                    sw.Write("not empty blah blah");
                using (StreamWriter sw = new StreamWriter(resxFile3))
                    sw.Write("");
                using (StreamWriter sw = new StreamWriter(resxFile4))
                    sw.Write("not empty blah blah");
                using (StreamWriter sw = new StreamWriter(resxFile5))
                    sw.Write("");
                using (StreamWriter sw = new StreamWriter(resxFile6))
                    sw.Write("not empty blah blah");

                ProjectFileConverter projectFileConverter = new ProjectFileConverter();
                projectFileConverter.OldProjectFile = projectFile;
                newProjectFile = projectFile + ".1";
                projectFileConverter.NewProjectFile = newProjectFile;
                projectFileConverter.IsUserFile = false;
                projectFileConverter.Convert(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

                string newContent;
                using (StreamReader sr = new StreamReader(newProjectFile))
                {
                    newContent = sr.ReadToEnd();
                }

                #region expectedContent
                string expectedContent = @"<ItemGroup>
    <Compile Include=""AssemblyInfo.cs"" />
    <Compile Include=""" + Path.GetFileNameWithoutExtension(resxFile3) + @".cs"">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include=""" + Path.GetFileNameWithoutExtension(resxFile2) + @".cs"">
      <SubType>Form</SubType>
    </Compile>
    <Content Include=""App.ico"" />
    <EmbeddedResource Include=""..\" + Path.GetFileName(resxFile6) + @""">
      <Link>resx6.resx</Link>
    </EmbeddedResource>
    <EmbeddedResource Include=""" + Path.GetFileName(resxFile2) + @""">
      <DependentUpon>" + Path.GetFileNameWithoutExtension(resxFile2) + @".cs</DependentUpon>
    </EmbeddedResource>
    <EmbeddedResource Include=""" + Path.GetFileName(resxFile4) + @""" />
  </ItemGroup>";
                #endregion

                Console.WriteLine("=actual=");
                Console.WriteLine(newContent);
                Console.WriteLine("==");
                Console.WriteLine("=expected=");
                Console.WriteLine(expectedContent);
                Console.WriteLine("==");

                Assert.AreNotEqual(-1, newContent.IndexOf(expectedContent));

                // shouldn't delete any
                Assert.IsTrue(File.Exists(resxFile1));
                Assert.IsTrue(File.Exists(resxFile2));
                Assert.IsTrue(File.Exists(resxFile3));
                Assert.IsTrue(File.Exists(resxFile4));
                Assert.IsTrue(File.Exists(resxFile5));
                Assert.IsTrue(File.Exists(resxFile6));

            }
            finally
            {
                if (File.Exists(projectFile)) File.Delete(projectFile);
                if (File.Exists(newProjectFile)) File.Delete(newProjectFile);
                if (File.Exists(resxFile1)) File.Delete(resxFile1);
                if (File.Exists(resxFile2)) File.Delete(resxFile2);
                if (File.Exists(resxFile3)) File.Delete(resxFile3);
                if (File.Exists(resxFile4)) File.Delete(resxFile4);
                if (File.Exists(resxFile5)) File.Delete(resxFile5);
                if (File.Exists(resxFile6)) File.Delete(resxFile6);
                if (Directory.Exists(projFolder)) Directory.Delete(projFolder);
            }
        }

        /// <summary>
        /// This is a regression test for bug VSWhidbey 414632.  We're testing to make sure that if the Everett
        /// project already had a value for MyType that the conversion utility should not overwrite it.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void VbConversionWithMyTypeAlreadySet()
        {
            // **********************************************
            //                   EVERETT 
            // **********************************************
            string everettProjectContents = @"
                <VisualStudioProject>
                    <VisualBasic
                        ProjectType = `Local`
                        MyType = `WindowsForms`
                        SchemaVersion = `2.0`
                    >
                        <Build>
                            <Settings
                                AssemblyName = `Project1`
                                OutputType = `WinExe`
                                StartupObject = `Project1.Form1`
                                AssemblyMajorVersion = `1`
                                AssemblyMinorVersion = `0`
                                AssemblyRevisionNumber = `0`
                                GenerateRevisionNumber = `False`
                                AssemblyCompanyName = `Microsoft`
                                RootNamespace = `Project1`
                            >
                                <Config
                                    Name = `Debug`
                                    DebugSymbols = `True`
                                    DefineDebug = `True`
                                    DefineTrace = `True`
                                    OutputPath = `.\bin\`
                                    DefineConstants = `Win32=True`
                                />
                                <Config
                                    Name = `Release`
                                    DebugSymbols = `False`
                                    DefineDebug = `False`
                                    DefineTrace = `True`
                                    OutputPath = `.\bin\`
                                    DefineConstants = `Win32=True`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                />
                                <Reference
                                    Name = `System.Data`
                                    AssemblyName = `System.Data`
                                />
                                <Reference
                                    Name = `System.Drawing`
                                    AssemblyName = `System.Drawing`
                                />
                                <Reference
                                    Name = `System.Windows.Forms`
                                    AssemblyName = `System.Windows.Forms`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                />
                                <Reference
                                    Name = `Microsoft.VisualBasic.Compatibility`
                                    AssemblyName = `Microsoft.VisualBasic.Compatibility`
                                />
                            </References>
                            <Imports>
                                <Import Namespace = `Microsoft.VisualBasic` />
                                <Import Namespace = `Microsoft.VisualBasic.Compatibility` />
                                <Import Namespace = `System` />
                                <Import Namespace = `System.Collections` />
                                <Import Namespace = `System.Data` />
                                <Import Namespace = `System.Drawing` />
                                <Import Namespace = `System.Diagnostics` />
                                <Import Namespace = `System.Windows.Forms` />
                            </Imports>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `Form1.Designer.vb`
                                    BuildAction = `Compile`
                                    SubType = `Code`
                                    DependentUpon = `Form1.vb`
                                />
                                <File
                                    RelPath = `Form1.vb`
                                    BuildAction = `Compile`
                                    SubType = `Form`
                                />
                                <File
                                    RelPath = `Form1.resX`
                                    DependentUpon = `Form1.vb`
                                    BuildAction = `EmbeddedResource`
                                />
                                <File
                                    RelPath = `AssemblyInfo.vb`
                                    BuildAction = `Compile`
                                />
                            </Include>
                        </Files>
                    </VisualBasic>
                </VisualStudioProject>
                ";

            // **********************************************
            //                   WHIDBEY
            // **********************************************
            string whidbeyProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <MyType>WindowsForms</MyType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <AssemblyName>Project1</AssemblyName>
                        <OutputType>WinExe</OutputType>
                        <StartupObject>Project1.Form1</StartupObject>
                        <AssemblyMajorVersion>1</AssemblyMajorVersion>
                        <AssemblyMinorVersion>0</AssemblyMinorVersion>
                        <AssemblyRevisionNumber>0</AssemblyRevisionNumber>
                        <GenerateRevisionNumber>False</GenerateRevisionNumber>
                        <AssemblyCompanyName>Microsoft</AssemblyCompanyName>
                        <RootNamespace>Project1</RootNamespace>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>.\bin\</OutputPath>
                        <DocumentationFile>Project1.xml</DocumentationFile>
                        <DebugSymbols>True</DebugSymbols>
                        <DefineDebug>True</DefineDebug>
                        <DefineTrace>True</DefineTrace>
                        <DefineConstants>Win32=True</DefineConstants>
                        <NoWarn>42016,42017,42018,42019,42032,42353,42354,42355</NoWarn>
                        <DebugType>full</DebugType>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>.\bin\</OutputPath>
                        <DocumentationFile>Project1.xml</DocumentationFile>
                        <DebugSymbols>False</DebugSymbols>
                        <DefineDebug>False</DefineDebug>
                        <DefineTrace>True</DefineTrace>
                        <DefineConstants>Win32=True</DefineConstants>
                        <NoWarn>42016,42017,42018,42019,42032,42353,42354,42355</NoWarn>
                        <DebugType>none</DebugType>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`Microsoft.VisualBasic.Compatibility`>
                        <Name>Microsoft.VisualBasic.Compatibility</Name>
                        </Reference>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.Data`>
                        <Name>System.Data</Name>
                        </Reference>
                        <Reference Include=`System.Drawing`>
                        <Name>System.Drawing</Name>
                        </Reference>
                        <Reference Include=`System.Windows.Forms`>
                        <Name>System.Windows.Forms</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                        <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Import Include=`Microsoft.VisualBasic` />
                        <Import Include=`Microsoft.VisualBasic.Compatibility` />
                        <Import Include=`System` />
                        <Import Include=`System.Collections` />
                        <Import Include=`System.Data` />
                        <Import Include=`System.Diagnostics` />
                        <Import Include=`System.Drawing` />
                        <Import Include=`System.Windows.Forms` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`AssemblyInfo.vb` />
                        <Compile Include=`Form1.Designer.vb`>
                        <SubType>Code</SubType>
                        <DependentUpon>Form1.vb</DependentUpon>
                        </Compile>
                        <Compile Include=`Form1.vb`>
                        <SubType>Form</SubType>
                        </Compile>
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.VisualBasic.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
            ";

            Helpers.ConvertAndCompare(everettProjectContents, whidbeyProjectContents, "Form1.resx");
        }

        /// <summary>
        /// This is a regression test for bug VSWhidbey 477054.  We're testing to make sure that if the Everett
        /// project was a Trinity project and has a reference to an Office document file in the UserProperties
        /// section, that we correctly add this as an item "None" in the converted project.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void ConvertTrinityWithOfficeDocumentFile()
        {
            // **********************************************
            //                   EVERETT 
            // **********************************************
            string everettProjectContents = @"
                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`
                        SchemaVersion = `2.0`
                        ProjectGuid = `{305E5110-3086-465A-A246-9D9718726777}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = ``
                                AssemblyKeyContainerName = ``
                                AssemblyName = `ExcelProject1`
                                AssemblyOriginatorKeyFile = ``
                                DefaultClientScript = `JScript`
                                DefaultHTMLPageLayout = `Grid`
                                DefaultTargetSchema = `IE50`
                                DelaySign = `false`
                                OutputType = `Library`
                                PreBuildEvent = ``
                                PostBuildEvent = ``
                                RootNamespace = `ExcelProject1`
                                RunPostBuildEvent = `OnBuildSuccess`
                                StartupObject = ``
                            >
                                <Config
                                    Name = `Debug`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `DEBUG;TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `true`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `false`
                                    OutputPath = `bin\Debug\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                                <Config
                                    Name = `Release`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `false`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `true`
                                    OutputPath = `bin\Release\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                />
                                <Reference
                                    Name = `System.Data`
                                    AssemblyName = `System.Data`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.Xml`
                                />
                                <Reference
                                    Name = `MSForms`
                                    Guid = `{0D452EE1-E08F-101A-852E-02608C4D0BB4}`
                                    VersionMajor = `2`
                                    VersionMinor = `0`
                                    Lcid = `0`
                                    WrapperTool = `primary`
                                />
                                <Reference
                                    Name = `System.Windows.Forms`
                                    AssemblyName = `System.Windows.Forms`
                                    HintPath = `..\..\WINNT\Microsoft.NET\Framework\v1.1.4322\System.Windows.Forms.dll`
                                />
                                <Reference
                                    Name = `Microsoft.Office.Core`
                                    Guid = `{2DF8D04C-5BFA-101B-BDE5-00AA0044DE52}`
                                    VersionMajor = `2`
                                    VersionMinor = `3`
                                    Lcid = `0`
                                    WrapperTool = `primary`
                                />
                                <Reference
                                    Name = `VBIDE`
                                    Guid = `{0002E157-0000-0000-C000-000000000046}`
                                    VersionMajor = `5`
                                    VersionMinor = `3`
                                    Lcid = `0`
                                    WrapperTool = `primary`
                                />
                                <Reference
                                    Name = `Excel`
                                    Guid = `{00020813-0000-0000-C000-000000000046}`
                                    VersionMajor = `1`
                                    VersionMinor = `5`
                                    Lcid = `0`
                                    WrapperTool = `primary`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `AssemblyInfo.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `ThisWorkbook.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                            </Include>
                        </Files>
                        <StartupServices>
                            <Service ID = `{8F52F2DD-5E8A-4BBE-AFA6-5B941C11EED1}` />
                        </StartupServices>
                        <UserProperties
                            OfficeDocumentPath = `.\EXCELPROJECT1.XLS`
                            OfficeProjectType = `XLS`
                            OfficeProject = `true`
                            TrustedAssembly = `c:\rajeev_temp_deleteme\ExcelProject1\ExcelProject1_bin\ExcelProject1.dll`
                        />
                    </CSHARP>
                </VisualStudioProject>
                ";

            // **********************************************
            //                   WHIDBEY
            // **********************************************
            string whidbeyProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{305E5110-3086-465A-A246-9D9718726777}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon />
                        <AssemblyKeyContainerName />
                        <AssemblyName>ExcelProject1</AssemblyName>
                        <AssemblyOriginatorKeyFile />
                        <DefaultClientScript>JScript</DefaultClientScript>
                        <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
                        <DefaultTargetSchema>IE50</DefaultTargetSchema>
                        <DelaySign>false</DelaySign>
                        <OutputType>Library</OutputType>
                        <RootNamespace>ExcelProject1</RootNamespace>
                        <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
                        <StartupObject />
                        <ProjectTypeGuids>{BAA0C2D2-18E2-41B9-852F-F413020CAA33};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>true</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>false</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>full</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>false</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>true</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>none</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <COMReference Include=`Excel`>
                            <Guid>{00020813-0000-0000-C000-000000000046}</Guid>
                            <VersionMajor>1</VersionMajor>
                            <VersionMinor>5</VersionMinor>
                            <Lcid>0</Lcid>
                            <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=`Microsoft.Office.Core`>
                            <Guid>{2DF8D04C-5BFA-101B-BDE5-00AA0044DE52}</Guid>
                            <VersionMajor>2</VersionMajor>
                            <VersionMinor>3</VersionMinor>
                            <Lcid>0</Lcid>
                            <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=`MSForms`>
                            <Guid>{0D452EE1-E08F-101A-852E-02608C4D0BB4}</Guid>
                            <VersionMajor>2</VersionMajor>
                            <VersionMinor>0</VersionMinor>
                            <Lcid>0</Lcid>
                            <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=`VBIDE`>
                            <Guid>{0002E157-0000-0000-C000-000000000046}</Guid>
                            <VersionMajor>5</VersionMajor>
                            <VersionMinor>3</VersionMinor>
                            <Lcid>0</Lcid>
                            <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <Reference Include=`System`>
                            <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.Data`>
                            <Name>System.Data</Name>
                        </Reference>
                        <Reference Include=`System.Windows.Forms`>
                            <Name>System.Windows.Forms</Name>
                        </Reference>
                        <Reference Include=`System.Xml`>
                            <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`AssemblyInfo.cs`>
                            <SubType>Code</SubType>
                        </Compile>
                        <Compile Include=`ThisWorkbook.cs`>
                            <SubType>Code</SubType>
                        </Compile>
                    </ItemGroup>
                    <ItemGroup>
                        <Service Include=`{8F52F2DD-5E8A-4BBE-AFA6-5B941C11EED1}` />
                    </ItemGroup>
                    <ItemGroup>
                        <None Include=`.\EXCELPROJECT1.XLS` />
                    </ItemGroup>
                    <ProjectExtensions>
                        <VisualStudio>
                            <UserProperties 
                                OfficeDocumentPath=`.\EXCELPROJECT1.XLS` 
                                OfficeProjectType=`XLS` 
                                OfficeProject=`true` 
                                TrustedAssembly=`c:\rajeev_temp_deleteme\ExcelProject1\ExcelProject1_bin\ExcelProject1.dll` 
                             />
                        </VisualStudio>
                    </ProjectExtensions>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
            ";

            Helpers.ConvertAndCompare(everettProjectContents, whidbeyProjectContents, "EXCELPROJECT1.XLS");
        }

        /// <summary>
        /// This is to test that we convert <Folder> nodes correctly.  It's not rocket
        /// science, but we need to cover it.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void ConvertEmptyFolders()
        {
            // **********************************************
            //                   EVERETT 
            // **********************************************
            string everettProjectContents = @"

                <VisualStudioProject>
                    <VisualBasic
                        ProjectType = `Local`
                        SchemaVersion = `2.0`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = ``
                            >
                                <Config
                                    Name = `Debug`
                                />
                                <Config
                                    Name = `Release`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                />
                            </References>
                            <Imports>
                                <Import Namespace = `Microsoft.VisualBasic` />
                                <Import Namespace = `System` />
                            </Imports>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `AssemblyInfo.vb`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Form1.vb`
                                    SubType = `Form`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Form1.resx`
                                    DependentUpon = `Form1.vb`
                                    BuildAction = `EmbeddedResource`
                                />
                                <Folder RelPath = `MySubFolder\` />
                            </Include>
                        </Files>
                    </VisualBasic>
                </VisualStudioProject>
                ";

            // **********************************************
            //                   WHIDBEY
            // **********************************************
            string whidbeyProjectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon />
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DocumentationFile>.xml</DocumentationFile>
                        <NoWarn>42016,42017,42018,42019,42032,42353,42354,42355</NoWarn>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DocumentationFile>.xml</DocumentationFile>
                        <NoWarn>42016,42017,42018,42019,42032,42353,42354,42355</NoWarn>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Import Include=`Microsoft.VisualBasic` />
                        <Import Include=`System` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`AssemblyInfo.vb`>
                        <SubType>Code</SubType>
                        </Compile>
                        <Compile Include=`Form1.vb`>
                        <SubType>Form</SubType>
                        </Compile>
                        <Folder Include=`MySubFolder` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.VisualBasic.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
            ";

            Helpers.ConvertAndCompare(everettProjectContents, whidbeyProjectContents, "Form1.resx");
        }

        /// <summary>
        /// This is to test that we convert P2P references correctly by looking up the
        /// referenced project in the passed-in .SLN file.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void ConvertP2PReference()
        {
            // **********************************************
            //                   EVERETT PROJECT
            // **********************************************
            string resxFile = Path.Combine(Path.GetTempPath(), "Form1.resx");
            File.WriteAllText(resxFile, String.Empty);

            string everettProjectFile = ObjectModelHelpers.CreateTempFileOnDiskNoFormat(@"

                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`
                        SchemaVersion = `2.0`
                        ProjectGuid = `{77E21864-797C-4220-974E-530BB832801B}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = `App.ico`
                            >
                                <Config
                                    Name = `Debug`
                                    OutputPath = `bin\Debug\`
                                />
                                <Config
                                    Name = `Release`
                                    OutputPath = `bin\Release\`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll`
                                />
                                <Reference
                                    Name = `ClassLibrary1`
                                    Project = `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
                                    Package = `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `Form1.cs`
                                    SubType = `Form`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Form1.resx`
                                    DependentUpon = `Form1.cs`
                                    BuildAction = `EmbeddedResource`
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ");

            // **********************************************
            //                   EVERETT SOLUTION
            // **********************************************
            string everettSolutionFile = ObjectModelHelpers.CreateTempFileOnDiskNoFormat(

                @"Microsoft Visual Studio Solution File, Format Version 8.00
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `WindowsApplication2`, `WindowsApplication2.csproj`, `{77E21864-797C-4220-974E-530BB832801B}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `ClassLibrary1`, `..\ClassLibrary1\ClassLibrary1.csproj`, `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Global
	                GlobalSection(SolutionConfiguration) = preSolution
		                Debug = Debug
		                Release = Release
	                EndGlobalSection
	                GlobalSection(ProjectConfiguration) = postSolution
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.ActiveCfg = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.Build.0 = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.ActiveCfg = Release|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.Build.0 = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.ActiveCfg = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.Build.0 = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.ActiveCfg = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.Build.0 = Release|.NET
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
	                EndGlobalSection
	                GlobalSection(ExtensibilityAddIns) = postSolution
	                EndGlobalSection
                EndGlobal
                ");

            // **********************************************
            //                   WHIDBEY PROJECT
            // **********************************************
            string whidbeyProjectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{77E21864-797C-4220-974E-530BB832801B}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon>App.ico</ApplicationIcon>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\ClassLibrary1\ClassLibrary1.csproj`>
                        <Name>ClassLibrary1</Name>
                        <Project>{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}</Project>
                        <Package>{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</Package>
                        </ProjectReference>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                        <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Form1.cs`>
                        <SubType>Form</SubType>
                        </Compile>
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
                ";

            ProjectFileConverter converter = new ProjectFileConverter();
            converter.OldProjectFile = everettProjectFile;
            converter.SolutionFile = everettSolutionFile;
            converter.IsUserFile = false;

            ProjectRootElement project = converter.ConvertInMemory();

            Helpers.CompareProjectXml(whidbeyProjectContents, project.RawXml);

            File.Delete(resxFile);
            File.Delete(everettProjectFile);
            File.Delete(everettSolutionFile);
        }

        /// <summary>
        /// This is to test that we convert Everett projects that are part of solutions including
        /// VC++ projects. 
        /// </summary>
        [TestMethod]
        public void ConvertProjectWithVCInSolutionAndP2Ps()
        {
            // **********************************************
            //                   EVERETT PROJECT
            // **********************************************
            string resxFile = Path.Combine(Path.GetTempPath(), "Form1.resx");
            File.WriteAllText(resxFile, String.Empty);

            string everettProjectFile = ObjectModelHelpers.CreateTempFileOnDiskNoFormat(@"

                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`
                        SchemaVersion = `2.0`
                        ProjectGuid = `{77E21864-797C-4220-974E-530BB832801B}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = `App.ico`
                            >
                                <Config
                                    Name = `Debug`
                                    OutputPath = `bin\Debug\`
                                />
                                <Config
                                    Name = `Release`
                                    OutputPath = `bin\Release\`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll`
                                />
                                <Reference
                                    Name = `ClassLibrary1`
                                    Project = `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
                                    Package = `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `Form1.cs`
                                    SubType = `Form`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Form1.resx`
                                    DependentUpon = `Form1.cs`
                                    BuildAction = `EmbeddedResource`
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ");

            // **********************************************
            //                   EVERETT SOLUTION
            // **********************************************
            string everettSolutionFile = ObjectModelHelpers.CreateTempFileOnDiskNoFormat(

                @"Microsoft Visual Studio Solution File, Format Version 8.00
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `WindowsApplication2`, `WindowsApplication2.csproj`, `{77E21864-797C-4220-974E-530BB832801B}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `ClassLibrary1`, `..\ClassLibrary1\ClassLibrary1.csproj`, `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Project(`{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}`) = `VCClassLibrary1`, `..\VCClassLibrary1\VCClassLibrary1.vcproj`, `{6C4A36F7-F091-432A-9CA0-98FF2DEA9F33}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Global
	                GlobalSection(SolutionConfiguration) = preSolution
		                Debug = Debug
		                Release = Release
	                EndGlobalSection
	                GlobalSection(ProjectConfiguration) = postSolution
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.ActiveCfg = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.Build.0 = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.ActiveCfg = Release|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.Build.0 = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.ActiveCfg = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.Build.0 = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.ActiveCfg = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.Build.0 = Release|.NET
		                {6C4A36F7-F091-432A-9CA0-98FF2DEA9F33}.Debug.ActiveCfg = Debug|.NET
		                {6C4A36F7-F091-432A-9CA0-98FF2DEA9F33}.Debug.Build.0 = Debug|.NET
		                {6C4A36F7-F091-432A-9CA0-98FF2DEA9F33}.Release.ActiveCfg = Release|.NET
		                {6C4A36F7-F091-432A-9CA0-98FF2DEA9F33}.Release.Build.0 = Release|.NET
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
	                EndGlobalSection
	                GlobalSection(ExtensibilityAddIns) = postSolution
	                EndGlobalSection
                EndGlobal
                ");

            // **********************************************
            //                   WHIDBEY PROJECT
            // **********************************************
            string whidbeyProjectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{77E21864-797C-4220-974E-530BB832801B}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon>App.ico</ApplicationIcon>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\ClassLibrary1\ClassLibrary1.csproj`>
                        <Name>ClassLibrary1</Name>
                        <Project>{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}</Project>
                        <Package>{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</Package>
                        </ProjectReference>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                        <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Form1.cs`>
                        <SubType>Form</SubType>
                        </Compile>
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
                ";

            ProjectFileConverter converter = new ProjectFileConverter();
            converter.OldProjectFile = everettProjectFile;
            converter.SolutionFile = everettSolutionFile;
            converter.IsUserFile = false;

            ProjectRootElement project = converter.ConvertInMemory();

            Helpers.CompareProjectXml(whidbeyProjectContents, project.RawXml);

            File.Delete(resxFile);
            File.Delete(everettProjectFile);
            File.Delete(everettSolutionFile);
        }
        
        /// <summary>
        /// This is to test that we convert P2P references correctly by looking up the
        /// referenced project in the given .SLN file.  Force the conversion code to
        /// have to search for the .SLN file.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void ConvertP2PReferenceSearchForSolution()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // **********************************************
            //                   EVERETT PROJECT
            // **********************************************
            string everettProjectFileRelativePath = @"Project\WindowsApplication1.csproj";
            ObjectModelHelpers.CreateFileInTempProjectDirectory(everettProjectFileRelativePath, @"

                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`
                        SchemaVersion = `2.0`
                        ProjectGuid = `{77E21864-797C-4220-974E-530BB832801B}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = `App.ico`
                            >
                                <Config
                                    Name = `Debug`
                                    OutputPath = `bin\Debug\`
                                />
                                <Config
                                    Name = `Release`
                                    OutputPath = `bin\Release\`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                    HintPath = `..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll`
                                />
                                <Reference
                                    Name = `ClassLibrary1`
                                    Project = `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
                                    Package = `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `Form1.cs`
                                    SubType = `Form`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Form1.resx`
                                    DependentUpon = `Form1.cs`
                                    BuildAction = `EmbeddedResource`
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ");

            string resxFile = "Project\\Form1.resx";
            ObjectModelHelpers.CreateFileInTempProjectDirectory(resxFile, String.Empty);

            // **********************************************
            //                   EVERETT SOLUTION
            // **********************************************
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"WindowsApplication1.sln", 

                @"Microsoft Visual Studio Solution File, Format Version 8.00
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `WindowsApplication1`, `Project\WindowsApplication1.csproj`, `{77E21864-797C-4220-974E-530BB832801B}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `ClassLibrary1`, `ClassLibrary1\ClassLibrary1.csproj`, `{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Global
	                GlobalSection(SolutionConfiguration) = preSolution
		                Debug = Debug
		                Release = Release
	                EndGlobalSection
	                GlobalSection(ProjectConfiguration) = postSolution
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.ActiveCfg = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Debug.Build.0 = Debug|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.ActiveCfg = Release|.NET
		                {77E21864-797C-4220-974E-530BB832801B}.Release.Build.0 = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.ActiveCfg = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Debug.Build.0 = Debug|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.ActiveCfg = Release|.NET
		                {F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}.Release.Build.0 = Release|.NET
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
	                EndGlobalSection
	                GlobalSection(ExtensibilityAddIns) = postSolution
	                EndGlobalSection
                EndGlobal
                ");

            // **********************************************
            //                   RANDOM OTHER SOLUTION
            // **********************************************
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Project\Random.sln", 

                @"Microsoft Visual Studio Solution File, Format Version 8.00
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `ClassLibrary2`, `ClassLibrary2\ClassLibrary2.csproj`, `{11111111-9E5C-4FE8-BE84-96F37D47F45A}`
	                ProjectSection(ProjectDependencies) = postProject
	                EndProjectSection
                EndProject
                Global
	                GlobalSection(SolutionConfiguration) = preSolution
		                Debug = Debug
		                Release = Release
	                EndGlobalSection
	                GlobalSection(ProjectConfiguration) = postSolution
		                {11111111-9E5C-4FE8-BE84-96F37D47F45A}.Debug.ActiveCfg = Debug|.NET
		                {11111111-9E5C-4FE8-BE84-96F37D47F45A}.Debug.Build.0 = Debug|.NET
		                {11111111-9E5C-4FE8-BE84-96F37D47F45A}.Release.ActiveCfg = Release|.NET
		                {11111111-9E5C-4FE8-BE84-96F37D47F45A}.Release.Build.0 = Release|.NET
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
	                EndGlobalSection
	                GlobalSection(ExtensibilityAddIns) = postSolution
	                EndGlobalSection
                EndGlobal
                ");

            // **********************************************
            //                   WHIDBEY PROJECT
            // **********************************************
            string whidbeyProjectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{77E21864-797C-4220-974E-530BB832801B}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon>App.ico</ApplicationIcon>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\ClassLibrary1\ClassLibrary1.csproj`>
                        <Name>ClassLibrary1</Name>
                        <Project>{F532DD6D-9E5C-4FE8-BE84-96F37D47F45A}</Project>
                        <Package>{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</Package>
                        </ProjectReference>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                        <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Form1.cs`>
                        <SubType>Form</SubType>
                        </Compile>
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
                </Project>
                ";

            ProjectFileConverter converter = new ProjectFileConverter();
            converter.OldProjectFile = Path.Combine(ObjectModelHelpers.TempProjectDir, everettProjectFileRelativePath);
            converter.IsUserFile = false;
            ProjectRootElement project = converter.ConvertInMemory();

            Helpers.CompareProjectXml(whidbeyProjectContents, project.RawXml);
        }
        
        /// <summary>
        /// This is a test for bug VSWhidbey 472064.  We are making sure that if DebugSymbols is true in
        /// the original project file, then we also emit DebugType to the whidbey project file.  We also 
        /// emit ErrorReport = prompt to the whidbey project file if the language is C#
        /// </summary>
        /// <owner>FaisalMo</owner>
        [TestMethod]
        public void ConvertEverettProjectWithNoDebugInfoFlag()
        {
            // **********************************************
            //                   EVERETT 
            // **********************************************
            string everettProjectContents = @"
                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`                        
                        SchemaVersion = `2.0`
                        ProjectGuid = `{172D0AFF-7BF3-4297-8168-792C46DC89DD}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = `App.ico`
                                AssemblyKeyContainerName = ``
                                AssemblyName = `ConsoleApplication1`
                                AssemblyOriginatorKeyFile = ``
                                DefaultClientScript = `JScript`
                                DefaultHTMLPageLayout = `Grid`
                                DefaultTargetSchema = `IE50`
                                DelaySign = `false`
                                OutputType = `Exe`
                                PreBuildEvent = ``
                                PostBuildEvent = ``
                                RootNamespace = `ConsoleApplication1`
                                RunPostBuildEvent = `OnBuildSuccess`
                                StartupObject = ``
                            >
                                <Config
                                    Name = `Debug`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `DEBUG;TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `true`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `false`
                                    OutputPath = `bin\Debug\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                                <Config
                                    Name = `Release`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `false`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `true`
                                    OutputPath = `bin\Release\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll`
                                />
                                <Reference
                                    Name = `System.Data`
                                    AssemblyName = `System.Data`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Data.dll`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `App.ico`
                                    BuildAction = `Content`
                                />
                                <File
                                    RelPath = `AssemblyInfo.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Class1.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ";

            // **********************************************
            //                   WHIDBEY
            // **********************************************
            string whidbeyProjectContents = @"
            <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{172D0AFF-7BF3-4297-8168-792C46DC89DD}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon>App.ico</ApplicationIcon>
                        <AssemblyKeyContainerName />
                        <AssemblyName>ConsoleApplication1</AssemblyName>
                        <AssemblyOriginatorKeyFile />
                        <DefaultClientScript>JScript</DefaultClientScript>
                        <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
                        <DefaultTargetSchema>IE50</DefaultTargetSchema>
                        <DelaySign>false</DelaySign>
                        <OutputType>Exe</OutputType>
                        <RootNamespace>ConsoleApplication1</RootNamespace>
                        <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
                        <StartupObject />
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>true</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>false</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>full</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>false</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>true</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>none</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System`>
                        <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.Data`>
                        <Name>System.Data</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                        <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`AssemblyInfo.cs`>
                        <SubType>Code</SubType>
                        </Compile>
                        <Compile Include=`Class1.cs`>
                        <SubType>Code</SubType>
                        </Compile>
                        <Content Include=`App.ico` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                    </PropertyGroup>
            </Project>
            ";

            Helpers.ConvertAndCompare(everettProjectContents, whidbeyProjectContents);
        }

        /// <summary>
        /// This is a test for converting Everett projects that had special characters in the AssemblyName,
        /// OutputPath, config name, etc.  These characters need to be escaped when converted to MSBuild.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void ConvertEverettProjectWithSpecialCharaceters()
        {
            // **********************************************
            //                   EVERETT 
            // **********************************************
            string everettProjectContents = @"
                <VisualStudioProject>
                    <CSHARP
                        ProjectType = `Local`                        
                        SchemaVersion = `2.0`
                        ProjectGuid = `{172D0AFF-7BF3-4297-8168-792C46DC89DD}`
                    >
                        <Build>
                            <Settings
                                ApplicationIcon = `App.ico`
                                AssemblyKeyContainerName = ``
                                AssemblyName = `Console;Application1`
                                AssemblyOriginatorKeyFile = ``
                                DefaultClientScript = `JScript`
                                DefaultHTMLPageLayout = `Grid`
                                DefaultTargetSchema = `IE50`
                                DelaySign = `false`
                                OutputType = `Exe`
                                PreBuildEvent = `echo $(TargetPath)`
                                PostBuildEvent = `echo %DEBUGGER%`
                                RootNamespace = `ConsoleApplication1`
                                RunPostBuildEvent = `OnBuildSuccess`
                                StartupObject = ``
                            >
                                <Config
                                    Name = `Debug`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `DEBUG;TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `true`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `false`
                                    OutputPath = `bin\Debu@foo - $(hello)g\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                                <Config
                                    Name = `Rajeev's Config`
                                    AllowUnsafeBlocks = `false`
                                    BaseAddress = `285212672`
                                    CheckForOverflowUnderflow = `false`
                                    ConfigurationOverrideFile = ``
                                    DefineConstants = `TRACE`
                                    DocumentationFile = ``
                                    DebugSymbols = `false`
                                    FileAlignment = `4096`
                                    IncrementalBuild = `false`
                                    NoStdLib = `false`
                                    NoWarn = ``
                                    Optimize = `true`
                                    OutputPath = `bin\Release\`
                                    RegisterForComInterop = `false`
                                    RemoveIntegerChecks = `false`
                                    TreatWarningsAsErrors = `false`
                                    WarningLevel = `4`
                                />
                            </Settings>
                            <References>
                                <Reference
                                    Name = `System`
                                    AssemblyName = `System`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll`
                                />
                                <Reference
                                    Name = `System.Data`
                                    AssemblyName = `System.Data`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Data.dll`
                                />
                                <Reference
                                    Name = `System.XML`
                                    AssemblyName = `System.XML`
                                    HintPath = `D:\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll`
                                />
                                <Reference
                                    Name = `Microsoft.My'Crazy;Assemb%ly`
                                    AssemblyName = `Microsoft.My'Crazy;Assemb%ly`
                                    HintPath = `D:\myapps\Microsoft.My'Crazy;Assemb%ly.dll`
                                />
                            </References>
                        </Build>
                        <Files>
                            <Include>
                                <File
                                    RelPath = `App.ico`
                                    BuildAction = `Content`
                                />
                                <File
                                    RelPath = `Assembly$Info.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                                <File
                                    RelPath = `Class1.cs`
                                    SubType = `Code`
                                    BuildAction = `Compile`
                                />
                            </Include>
                        </Files>
                    </CSHARP>
                </VisualStudioProject>
                ";

            // **********************************************
            //                   WHIDBEY
            // **********************************************
            string whidbeyProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`Build`>
                    <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{172D0AFF-7BF3-4297-8168-792C46DC89DD}</ProjectGuid>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ApplicationIcon>App.ico</ApplicationIcon>
                        <AssemblyKeyContainerName />
                        <AssemblyName>Console%3bApplication1</AssemblyName>
                        <AssemblyOriginatorKeyFile />
                        <DefaultClientScript>JScript</DefaultClientScript>
                        <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
                        <DefaultTargetSchema>IE50</DefaultTargetSchema>
                        <DelaySign>false</DelaySign>
                        <OutputType>Exe</OutputType>
                        <RootNamespace>ConsoleApplication1</RootNamespace>
                        <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
                        <StartupObject />
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debu%40foo - %24%28hello%29g\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>true</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>false</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>full</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Rajeev%27s Config|AnyCPU' `>
                        <OutputPath>bin\Release\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>false</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>true</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>none</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`Microsoft.My%27Crazy%3bAssemb%25ly`>
                            <Name>Microsoft.My%27Crazy%3bAssemb%25ly</Name>
                            <HintPath>D:\myapps\Microsoft.My%27Crazy%3bAssemb%25ly.dll</HintPath>
                        </Reference>
                        <Reference Include=`System`>
                            <Name>System</Name>
                        </Reference>
                        <Reference Include=`System.Data`>
                            <Name>System.Data</Name>
                        </Reference>
                        <Reference Include=`System.XML`>
                            <Name>System.XML</Name>
                        </Reference>
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Assembly%24Info.cs`>
                            <SubType>Code</SubType>
                        </Compile>
                        <Compile Include=`Class1.cs`>
                            <SubType>Code</SubType>
                        </Compile>
                        <Content Include=`App.ico` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    <PropertyGroup>
                        <PreBuildEvent>echo $(TargetPath)</PreBuildEvent>
                        <PostBuildEvent>echo %25DEBUGGER%25</PostBuildEvent>
                    </PropertyGroup>
                </Project>
            ";

            Helpers.ConvertAndCompare(everettProjectContents, whidbeyProjectContents);
        }

        /// <summary>
        /// Dev10 Bug 557388: When converting a project to v4.0, if the project contains 
        /// references to v3.5 and before VC projects (.vcproj), convert that reference to 
        /// instead reference a .vcxproj of the same name. 
        /// </summary>
        [TestMethod]
        public void ConvertVCProjectReferenceExtensions()
        {
            string orcasProjectFileContents = @"
                <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" DefaultTargets=""t"">
                    <ItemGroup>
                        <ProjectReference Include=""a.vcproj"" />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            string dev10projectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`t`>
                    <ItemGroup>
                        <ProjectReference Include=`a.vcxproj` />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            Helpers.ConvertAndCompare(orcasProjectFileContents, dev10projectContents);
        }

        /// <summary>
        /// Dev10 Bug 557388: When converting a project to v4.0, if the project contains 
        /// references to v3.5 and before VC projects (.vcproj), convert that reference to 
        /// instead reference a .vcxproj of the same name. 
        /// </summary>
        [TestMethod]
        public void ConvertVCProjectReferenceExtensionsWildcard()
        {
            string orcasProjectFileContents = @"
                <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" DefaultTargets=""t"">
                    <ItemGroup>
                        <ProjectReference Include=""*.vcproj"" />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            string dev10projectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`t`>
                    <ItemGroup>
                        <ProjectReference Include=`*.vcxproj` />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            Helpers.ConvertAndCompare(orcasProjectFileContents, dev10projectContents);
        }

        /// <summary>
        /// Dev10 Bug 557388: When converting a project to v4.0, if the project contains 
        /// references to v3.5 and before VC projects (.vcproj), convert that reference to 
        /// instead reference a .vcxproj of the same name. 
        /// </summary>
        [TestMethod]
        public void ConvertVCProjectReferenceExtensionsTrimNeeded()
        {
            string orcasProjectFileContents = @"
                <Project ToolsVersion=""3.5"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" DefaultTargets=""t"">
                    <ItemGroup>
                        <ProjectReference Include=""a.vcproj   "" />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            string dev10projectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace` DefaultTargets=`t`>
                    <ItemGroup>
                        <ProjectReference Include=`a.vcxproj` />
                    </ItemGroup>
                    <Target Name=""t"" />
                </Project>";

            Helpers.ConvertAndCompare(orcasProjectFileContents, dev10projectContents);
        }

        [TestMethod]
        public void ConvertProjectFileWithClientSubset()
        {
            string orcasProjectFileClient = @"
                <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.21022</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{3D948CB8-F515-41D5-B0ED-4215ED0A6D76}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>FxProfile</RootNamespace>
                    <AssemblyName>FxProfile</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <TargetFrameworkSubset>Client</TargetFrameworkSubset>
                    <FileAlignment>512</FileAlignment>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Xml"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.cs"" />
                    <Compile Include=""Properties\AssemblyInfo.cs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                </Project>
                ";

            string dev12ProjectFileClient = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.21022</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{3D948CB8-F515-41D5-B0ED-4215ED0A6D76}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>FxProfile</RootNamespace>
                    <AssemblyName>FxProfile</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Xml"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.cs"" />
                    <Compile Include=""Properties\AssemblyInfo.cs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                </Project>
                ");

            Helpers.ConvertAndCompare(orcasProjectFileClient, dev12ProjectFileClient);
        }

        [TestMethod]
        public void ConvertProjectFileWithFullSubset()
        {
            string orcasProjectFileFull = @"
                <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.21022</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{3D948CB8-F515-41D5-B0ED-4215ED0A6D76}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>FxProfile</RootNamespace>
                    <AssemblyName>FxProfile</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <TargetFrameworkSubset>Full</TargetFrameworkSubset>
                    <FileAlignment>512</FileAlignment>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Xml"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.cs"" />
                    <Compile Include=""Properties\AssemblyInfo.cs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                </Project>
                ";

            string dev12ProjectFileFull = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.21022</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{3D948CB8-F515-41D5-B0ED-4215ED0A6D76}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>FxProfile</RootNamespace>
                    <AssemblyName>FxProfile</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Xml"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.cs"" />
                    <Compile Include=""Properties\AssemblyInfo.cs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                </Project>
                ");

            Helpers.ConvertAndCompare(orcasProjectFileFull, dev12ProjectFileFull);
        }

        [TestMethod]
        public void ConvertFSharpOrcasProjectFile()
        {
            string fsharpOrcasProjectFile = @"
                <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{4516a96a-098b-408b-b914-71d82752c1cb}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ImportToDev10</RootNamespace>
                    <AssemblyName>ImportToDev10</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                    <Name>ImportToDev10</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>3</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>3</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildExtensionsPath)\FSharp\1.0\Microsoft.FSharp.Targets"" />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                ";
            string fsharpDev10ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{4516a96a-098b-408b-b914-71d82752c1cb}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ImportToDev10</RootNamespace>
                    <AssemblyName>ImportToDev10</AssemblyName>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                    <Name>ImportToDev10</Name>
                    <TargetFSharpCoreVersion>2.3.0.0</TargetFSharpCoreVersion>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>3</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>3</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core, Version=$(TargetFSharpCoreVersion), Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
                      <Private>True</Private>
                    </Reference>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                ";
            Helpers.ConvertAndCompare(fsharpOrcasProjectFile, fsharpDev10ProjectFile);
        }

        [TestMethod]
        public void ConvertDev11PortableLibraryProjectFile()
        {
            string sampleDev11PortableLibraryProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>abe4e969-375e-463c-8db0-005e32337771</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <RootNamespace>PortableLibrary1</RootNamespace>
                    <AssemblyName>PortableLibrary1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Profile47</TargetFrameworkProfile>
                    <Name>PortableLibrary1</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Debug\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Release\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core"">
                      <Name>FSharp.Core</Name>
                      <AssemblyName>FSharp.Core.dll</AssemblyName>
                      <HintPath>$(MSBuildExtensionsPath32)\..\Reference Assemblies\Microsoft\FSharp\3.0\Runtime\.NETPortable\FSharp.Core.dll</HintPath>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""PortableLibrary1.fs"" />
                    <None Include=""Script.fsx"" />
                  </ItemGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.Portable.FSharp.Targets"" />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
                ";

            string asDev12ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>abe4e969-375e-463c-8db0-005e32337771</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <RootNamespace>PortableLibrary1</RootNamespace>
                    <AssemblyName>PortableLibrary1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Profile47</TargetFrameworkProfile>
                    <Name>PortableLibrary1</Name>
                    <TargetFSharpCoreVersion>2.3.5.0</TargetFSharpCoreVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Debug\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Release\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core"">
                      <HintPath>$(MSBuildExtensionsPath32)\..\Reference Assemblies\Microsoft\FSharp\.NETPortable\$(TargetFSharpCoreVersion)\FSharp.Core.dll</HintPath>
                      <Private>True</Private>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""PortableLibrary1.fs"" />
                    <None Include=""Script.fsx"" />
                  </ItemGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.Portable.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.Portable.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
                ";

            Helpers.ConvertAndCompare(sampleDev11PortableLibraryProjectFile, asDev12ProjectFile);
        }

        [TestMethod]
        public void ConvertDev12PortableLibraryProjectFileShouldBeNoOp()
        { 
            string asDev12ProjectFile = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>abe4e969-375e-463c-8db0-005e32337771</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <RootNamespace>PortableLibrary1</RootNamespace>
                    <AssemblyName>PortableLibrary1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Profile47</TargetFrameworkProfile>
                    <Name>PortableLibrary1</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Debug\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <DocumentationFile>bin\Release\PortableLibrary1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core"">
                      <Name>FSharp.Core</Name>
                      <AssemblyName>FSharp.Core.dll</AssemblyName>
                      <HintPath>$(MSBuildExtensionsPath32)\..\Reference Assemblies\Microsoft\FSharp\3.0\Runtime\.NETPortable\FSharp.Core.dll</HintPath>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""PortableLibrary1.fs"" />
                    <None Include=""Script.fsx"" />
                  </ItemGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <PropertyGroup>
                    <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.Portable.FSharp.Targets</FSharpTargetsPath>
                  </PropertyGroup>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
                ");

            Helpers.ConvertAndCompare(asDev12ProjectFile, asDev12ProjectFile);
        }

        [TestMethod]
        public void ConvertFSharpDev10ProjectFile()
        {
            // pick standard proj from dev10, do conversion
            string sampleFSharpDev10ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{83a887ac-cd45-4d4a-a769-324d9055ac97}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication1</RootNamespace>
                    <AssemblyName>ConsoleApplication1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication1</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets"" Condition=""!Exists('$(MSBuildBinPath)\Microsoft.Build.Tasks.v4.0.dll')"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets"" Condition="" Exists('$(MSBuildBinPath)\Microsoft.Build.Tasks.v4.0.dll')"" />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                    ";
            string asDev11ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{83a887ac-cd45-4d4a-a769-324d9055ac97}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication1</RootNamespace>
                    <AssemblyName>ConsoleApplication1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication1</Name>
                    <TargetFSharpCoreVersion>4.3.0.0</TargetFSharpCoreVersion>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core, Version=$(TargetFSharpCoreVersion), Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
                      <Private>True</Private>
                    </Reference>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                ";
            Helpers.ConvertAndCompare(sampleFSharpDev10ProjectFile, asDev11ProjectFile);
        }

        [TestMethod]
        public void ConvertFSharpDev10ProjectFileWithMinVS()
        {
            // pick standard proj from dev10, do conversion
            string sampleFSharpDev10ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{83a887ac-cd45-4d4a-a769-324d9055ac97}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication1</RootNamespace>
                    <AssemblyName>ConsoleApplication1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication1</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">10</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <Import Project=""$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets"" Condition=""!Exists('$(MSBuildBinPath)\Microsoft.Build.Tasks.v4.0.dll')"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets"" Condition="" Exists('$(MSBuildBinPath)\Microsoft.Build.Tasks.v4.0.dll')"" />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                    ";
            string asDev11ProjectFile = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <ProductVersion>8.0.30703</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{83a887ac-cd45-4d4a-a769-324d9055ac97}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication1</RootNamespace>
                    <AssemblyName>ConsoleApplication1</AssemblyName>
                    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication1</Name>
                    <TargetFSharpCoreVersion>4.3.0.0</TargetFSharpCoreVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication1.XML</DocumentationFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core, Version=$(TargetFSharpCoreVersion), Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
                      <Private>True</Private>
                    </Reference>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                  </ItemGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">10</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                         Other similar extension points exist, see Microsoft.Common.targets.
                    <Target Name=""BeforeBuild"">
                    </Target>
                    <Target Name=""AfterBuild"">
                    </Target>
                    -->
                </Project>
                ";
            Helpers.ConvertAndCompare(sampleFSharpDev10ProjectFile, asDev11ProjectFile);
        }
        
        [TestMethod]
        public void ConvertFSharpDev11ProjectFile()
        {
            string sampleDev11Project = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{bfafbde5-287f-430e-85ed-e5cdfc71213b}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication2</RootNamespace>
                    <AssemblyName>ConsoleApplication2</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication2</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets"" Condition=""Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets"" Condition=""(!Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')) And (Exists('$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets'))"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets"" Condition=""(!Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')) And (!Exists('$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets')) And (Exists('$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets'))"" />
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                    <None Include=""App.config"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
            ";

            string aDev12Project = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{bfafbde5-287f-430e-85ed-e5cdfc71213b}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication2</RootNamespace>
                    <AssemblyName>ConsoleApplication2</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication2</Name>
                    <TargetFSharpCoreVersion>4.3.0.0</TargetFSharpCoreVersion>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                    <None Include=""App.config"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Reference Include=""FSharp.Core, Version=$(TargetFSharpCoreVersion), Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
                      <Private>True</Private>
                    </Reference>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
            ";
            Helpers.ConvertAndCompare(sampleDev11Project, aDev12Project);
        }

        [TestMethod]
        public void ConvertFSharpDev11ProjectFileWithCustomFSharpCoreLocation()
        {
            string sampleDev11Project = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{bfafbde5-287f-430e-85ed-e5cdfc71213b}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication2</RootNamespace>
                    <AssemblyName>ConsoleApplication2</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication2</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets"" Condition=""Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets"" Condition=""(!Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')) And (Exists('$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets'))"" />
                  <Import Project=""$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets"" Condition=""(!Exists('$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets')) And (!Exists('$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets')) And (Exists('$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets'))"" />
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                    <None Include=""App.config"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"">
                        <HintPath>c:\some-custom-location\FSharp.Core.dll</HintPath>
                    </Reference>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
            ";

            string aDev12Project = @"
                <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{bfafbde5-287f-430e-85ed-e5cdfc71213b}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication2</RootNamespace>
                    <AssemblyName>ConsoleApplication2</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication2</Name>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">11</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <Choose>
                    <When Condition=""'$(VisualStudioVersion)' == '11.0'"">
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup>
                        <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                      </PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                    <None Include=""App.config"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"">
                        <HintPath>c:\some-custom-location\FSharp.Core.dll</HintPath>
                    </Reference>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
            ";
            Helpers.ConvertAndCompare(sampleDev11Project, aDev12Project);
        }

        [TestMethod]
        public void ConvertFSharpDev12ProjectFileShouldBeNoOp()
        {
            string aDev12Project = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{bfafbde5-287f-430e-85ed-e5cdfc71213b}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <RootNamespace>ConsoleApplication2</RootNamespace>
                    <AssemblyName>ConsoleApplication2</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                    <Name>ConsoleApplication2</Name>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <Tailcalls>false</Tailcalls>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Debug\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <Tailcalls>true</Tailcalls>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <WarningLevel>3</WarningLevel>
                    <PlatformTarget>x86</PlatformTarget>
                    <DocumentationFile>bin\Release\ConsoleApplication2.XML</DocumentationFile>
                  </PropertyGroup>
                  <PropertyGroup>
                      <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                  </PropertyGroup>
                  <PropertyGroup>
                    <MinimumVisualStudioVersion Condition=""'$(MinimumVisualStudioVersion)' == ''"">12</MinimumVisualStudioVersion>
                  </PropertyGroup>
                  <Import Project=""$(FSharpTargetsPath)"" Condition=""Exists('$(FSharpTargetsPath)')""/>
                  <ItemGroup>
                    <Compile Include=""Program.fs"" />
                    <None Include=""App.config"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Reference Include=""mscorlib"" />
                    <Reference Include=""FSharp.Core"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Core"" />
                    <Reference Include=""System.Numerics"" />
                  </ItemGroup>
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
                </Project>
            ");
            Helpers.ConvertAndCompare(aDev12Project, aDev12Project); // ensure no change
        }

        [TestMethod]
        public void ConvertWFProjectFile()
        {
            string wfWhidbeyProjectFile = @"
                <Project DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <ProductVersion>8.0.50727</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{60A2C065-FE5A-4A8F-808D-506BE3007BBD}</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <RootNamespace>WorkflowLibrary1</RootNamespace>
                    <AssemblyName>WorkflowLibrary1</AssemblyName>
                    <ProjectTypeGuids>{14822709-B5A1-4724-98CA-57A101D1B079};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)' == 'Debug' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>.\bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <UseVSHostingProcess>false</UseVSHostingProcess>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)' == 'Release' "">
                    <DebugSymbols>false</DebugSymbols>
                    <Optimize>true</Optimize>
                    <OutputPath>.\bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <UseVSHostingProcess>false</UseVSHostingProcess>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System.Workflow.Activities"" />
                    <Reference Include=""System.Workflow.ComponentModel"" />
                    <Reference Include=""System.Workflow.Runtime"" />
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Design"" />
                    <Reference Include=""System.Drawing"" />
                    <Reference Include=""System.Drawing.Design"" />
                    <Reference Include=""System.Transactions"" />
                    <Reference Include=""System.Xml"" />
                    <Reference Include=""System.Web"" />
                    <Reference Include=""System.Web.Services"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Properties\AssemblyInfo.cs"" />
                    <None Include=""Properties\Settings.settings"">
                      <Generator>SettingsSingleFileGenerator</Generator>
                      <LastGenOutput>Settings.cs</LastGenOutput>
                    </None>
                    <Compile Include=""Workflow1.cs"">
                      <SubType>Component</SubType>
                    </Compile>
                    <Compile Include=""Workflow1.designer.cs"">
                      <DependentUpon>Workflow1.cs</DependentUpon>
                    </Compile>
                    <Compile Include=""Properties\Settings.cs"">
                      <AutoGen>True</AutoGen>
                      <DependentUpon>Settings.settings</DependentUpon>
                    </Compile>
                    <AppDesigner Include=""Properties\"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.Targets"" />
                  <Import Project=""$(MSBuildExtensionsPath)\Microsoft\Windows Workflow Foundation\v3.0\Workflow.Targets"" />
                </Project>                
                    ";
            string wfDev10ProjectFile = ObjectModelHelpers.CleanupFileContents(@"
                    <Project DefaultTargets=""Build"" xmlns=""msbuildnamespace"" ToolsVersion=""msbuilddefaulttoolsversion"">
                      <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <ProductVersion>8.0.50727</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{60A2C065-FE5A-4A8F-808D-506BE3007BBD}</ProjectGuid>
                        <OutputType>Library</OutputType>
                        <RootNamespace>WorkflowLibrary1</RootNamespace>
                        <AssemblyName>WorkflowLibrary1</AssemblyName>
                        <ProjectTypeGuids>{14822709-B5A1-4724-98CA-57A101D1B079};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                        <WarningLevel>4</WarningLevel>
                        <TargetFrameworkVersion>v3.0</TargetFrameworkVersion>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)' == 'Debug' "">
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>.\bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <UseVSHostingProcess>false</UseVSHostingProcess>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)' == 'Release' "">
                        <DebugSymbols>false</DebugSymbols>
                        <Optimize>true</Optimize>
                        <OutputPath>.\bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <UseVSHostingProcess>false</UseVSHostingProcess>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include=""System.Workflow.Activities"" />
                        <Reference Include=""System.Workflow.ComponentModel"" />
                        <Reference Include=""System.Workflow.Runtime"" />
                        <Reference Include=""System"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Design"" />
                        <Reference Include=""System.Drawing"" />
                        <Reference Include=""System.Drawing.Design"" />
                        <Reference Include=""System.Transactions"" />
                        <Reference Include=""System.Xml"" />
                        <Reference Include=""System.Web"" />
                        <Reference Include=""System.Web.Services"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""Properties\AssemblyInfo.cs"" />
                        <None Include=""Properties\Settings.settings"">
                          <Generator>SettingsSingleFileGenerator</Generator>
                          <LastGenOutput>Settings.cs</LastGenOutput>
                        </None>
                        <Compile Include=""Workflow1.cs"">
                          <SubType>Component</SubType>
                        </Compile>
                        <Compile Include=""Workflow1.designer.cs"">
                          <DependentUpon>Workflow1.cs</DependentUpon>
                        </Compile>
                        <Compile Include=""Properties\Settings.cs"">
                          <AutoGen>True</AutoGen>
                          <DependentUpon>Settings.settings</DependentUpon>
                        </Compile>
                        <AppDesigner Include=""Properties\"" />
                      </ItemGroup>
                      <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.Targets"" />
                      <Import Project=""$(MSBuildToolsPath)\Workflow.Targets"" />
                    </Project>
                ");
            Helpers.ConvertAndCompare(wfWhidbeyProjectFile, wfDev10ProjectFile);
        }

        [TestMethod]
        public void ConvertVisualBasicVS2005Project() {
            string wfWhidbeyProjectFile = @"
                <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.30729</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{280AE511-8349-4296-8296-9EF098121639}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <StartupObject>ConsoleApplication21.Module1</StartupObject>
                    <RootNamespace>ConsoleApplication21</RootNamespace>
                    <AssemblyName>ConsoleApplication21</AssemblyName>
                    <FileAlignment>512</FileAlignment>
                    <MyType>Console</MyType>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <OptionExplicit>On</OptionExplicit>
                    <OptionCompare>Binary</OptionCompare>
                    <OptionStrict>Off</OptionStrict>
                    <OptionInfer>On</OptionInfer>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <DefineDebug>true</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DocumentationFile>ConsoleApplication21.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <DefineDebug>false</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DocumentationFile>ConsoleApplication21.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Deployment"" />
                    <Reference Include=""System.Xml"" />
                    <Reference Include=""System.Core"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                    <Reference Include=""System.Xml.Linq"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                    <Reference Include=""System.Data.DataSetExtensions"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Import Include=""Microsoft.VisualBasic"" />
                    <Import Include=""System"" />
                    <Import Include=""System.Collections"" />
                    <Import Include=""System.Collections.Generic"" />
                    <Import Include=""System.Data"" />
                    <Import Include=""System.Diagnostics"" />
                    <Import Include=""System.Linq"" />
                    <Import Include=""System.Xml.Linq"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Module1.vb"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.VisualBasic.targets"" />
                </Project>
                ";
            string wfDev10ProjectFile = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>9.0.30729</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{280AE511-8349-4296-8296-9EF098121639}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <StartupObject>ConsoleApplication21.Module1</StartupObject>
                    <RootNamespace>ConsoleApplication21</RootNamespace>
                    <AssemblyName>ConsoleApplication21</AssemblyName>
                    <FileAlignment>512</FileAlignment>
                    <MyType>Console</MyType>
                    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
                    <OptionExplicit>On</OptionExplicit>
                    <OptionCompare>Binary</OptionCompare>
                    <OptionStrict>Off</OptionStrict>
                    <OptionInfer>On</OptionInfer>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <DefineDebug>true</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DocumentationFile>ConsoleApplication21.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022,42353,42354,42355</NoWarn>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <DefineDebug>false</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DocumentationFile>ConsoleApplication21.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022,42353,42354,42355</NoWarn>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Deployment"" />
                    <Reference Include=""System.Xml"" />
                    <Reference Include=""System.Core"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                    <Reference Include=""System.Xml.Linq"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                    <Reference Include=""System.Data.DataSetExtensions"">
                      <RequiredTargetFramework>3.5</RequiredTargetFramework>
                    </Reference>
                  </ItemGroup>
                  <ItemGroup>
                    <Import Include=""Microsoft.VisualBasic"" />
                    <Import Include=""System"" />
                    <Import Include=""System.Collections"" />
                    <Import Include=""System.Collections.Generic"" />
                    <Import Include=""System.Data"" />
                    <Import Include=""System.Diagnostics"" />
                    <Import Include=""System.Linq"" />
                    <Import Include=""System.Xml.Linq"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Module1.vb"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.VisualBasic.targets"" />
                </Project>
            ");
            Helpers.ConvertAndCompare(wfWhidbeyProjectFile, wfDev10ProjectFile);
        }

        #region VS2011

        /// <summary>
        /// A VS2010 C# project file which requires Repair
        /// </summary>
        [TestMethod]
        public void ConvertVB2008RepairRequired()
        {
            string ProjectBefore = @"
<Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProductVersion>9.0.30729</ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{C7234361-F078-473B-BCA0-2E62A6DE4D46}</ProjectGuid>
    <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{F184B08F-C81C-45F6-A57F-5ABD9991F28F}</ProjectTypeGuids>
    <OutputType>Library</OutputType>
    <RootNamespace>Vs2008Sp1_Wap_35_Vb</RootNamespace>
    <AssemblyName>Vs2008Sp1_Wap_35_Vb</AssemblyName>
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <MyType>Custom</MyType>
    <OptionExplicit>On</OptionExplicit>
    <OptionCompare>Binary</OptionCompare>
    <OptionStrict>Off</OptionStrict>
    <OptionInfer>On</OptionInfer>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <DefineDebug>true</DefineDebug>
    <DefineTrace>true</DefineTrace>
    <OutputPath>bin\</OutputPath>
    <DocumentationFile>Vs2008Sp1_Wap_35_Vb.xml</DocumentationFile>
    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <DefineDebug>false</DefineDebug>
    <DefineTrace>true</DefineTrace>
    <Optimize>true</Optimize>
    <OutputPath>bin\</OutputPath>
    <DocumentationFile>Vs2008Sp1_Wap_35_Vb.xml</DocumentationFile>
    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Drawing"" />
    <Reference Include=""System.Core"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Data.DataSetExtensions"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Web.Extensions"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Xml.Linq"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Web"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""System.Configuration"" />
    <Reference Include=""System.Web.Services"" />
    <Reference Include=""System.EnterpriseServices"" />
    <Reference Include=""System.Web.Mobile"" />
  </ItemGroup>
  <ItemGroup>
    <Import Include=""Microsoft.VisualBasic"" />
    <Import Include=""System"" />
    <Import Include=""System.Collections"" />
    <Import Include=""System.Collections.Generic"" />
    <Import Include=""System.Data"" />
    <Import Include=""System.Linq"" />
    <Import Include=""System.Xml.Linq"" />
    <Import Include=""System.Diagnostics"" />
    <Import Include=""System.Collections.Specialized"" />
    <Import Include=""System.Configuration"" />
    <Import Include=""System.Text"" />
    <Import Include=""System.Text.RegularExpressions"" />
    <Import Include=""System.Web"" />
    <Import Include=""System.Web.Caching"" />
    <Import Include=""System.Web.SessionState"" />
    <Import Include=""System.Web.Security"" />
    <Import Include=""System.Web.Profile"" />
    <Import Include=""System.Web.UI"" />
    <Import Include=""System.Web.UI.WebControls"" />
    <Import Include=""System.Web.UI.WebControls.WebParts"" />
    <Import Include=""System.Web.UI.HtmlControls"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Default.aspx"" />
    <Content Include=""Web.config"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Default.aspx.vb"">
      <SubType>ASPXCodeBehind</SubType>
      <DependentUpon>Default.aspx</DependentUpon>
    </Compile>
    <Compile Include=""Default.aspx.designer.vb"">
      <DependentUpon>Default.aspx</DependentUpon>
    </Compile>
    <Compile Include=""My Project\AssemblyInfo.vb"" />
    <Compile Include=""My Project\Application.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DependentUpon>Application.myapp</DependentUpon>
    </Compile>
    <Compile Include=""My Project\MyExtensions\MyWebExtension.vb"">
      <VBMyExtensionTemplateID>Microsoft.VisualBasic.Web.MyExtension</VBMyExtensionTemplateID>
      <VBMyExtensionTemplateVersion>1.0.0.0</VBMyExtensionTemplateVersion>
    </Compile>
    <Compile Include=""My Project\Resources.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>Resources.resx</DependentUpon>
    </Compile>
    <Compile Include=""My Project\Settings.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DependentUpon>Settings.settings</DependentUpon>
      <DesignTimeSharedInput>True</DesignTimeSharedInput>
    </Compile>
    <Compile Include=""Site1.Master.designer.vb"">
      <DependentUpon>Site1.Master</DependentUpon>
    </Compile>
    <Compile Include=""Site1.Master.vb"">
      <DependentUpon>Site1.Master</DependentUpon>
      <SubType>ASPXCodeBehind</SubType>
    </Compile>
    <Compile Include=""WebForm1.aspx.designer.vb"">
      <DependentUpon>WebForm1.aspx</DependentUpon>
    </Compile>
    <Compile Include=""WebForm1.aspx.vb"">
      <DependentUpon>WebForm1.aspx</DependentUpon>
      <SubType>ASPXCodebehind</SubType>
    </Compile>
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include=""My Project\Resources.resx"">
      <Generator>VbMyResourcesResXFileCodeGenerator</Generator>
      <LastGenOutput>Resources.Designer.vb</LastGenOutput>
      <CustomToolNamespace>My.Resources</CustomToolNamespace>
      <SubType>Designer</SubType>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup>
    <None Include=""My Project\Application.myapp"">
      <Generator>MyApplicationCodeGenerator</Generator>
      <LastGenOutput>Application.Designer.vb</LastGenOutput>
    </None>
    <None Include=""My Project\Settings.settings"">
      <Generator>SettingsSingleFileGenerator</Generator>
      <CustomToolNamespace>My</CustomToolNamespace>
      <LastGenOutput>Settings.Designer.vb</LastGenOutput>
    </None>
    <Content Include=""Site1.Master"" />
    <Content Include=""WebForm1.aspx"" />
  </ItemGroup>
  <ItemGroup>
    <Folder Include=""App_Data\"" />
  </ItemGroup>
  <Import Project=""$(MSBuildBinPath)\Microsoft.VisualBasic.targets"" />
  <Import Project=""$(MSBuildExtensionsPath)\Microsoft\VisualStudio\v9.0\WebApplications\Microsoft.WebApplication.targets"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
  <ProjectExtensions>
    <VisualStudio>
      <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
        <WebProjectProperties>
          <UseIIS>False</UseIIS>
          <AutoAssignPort>True</AutoAssignPort>
          <DevelopmentServerPort>64194</DevelopmentServerPort>
          <DevelopmentServerVPath>/</DevelopmentServerVPath>
          <IISUrl>
          </IISUrl>
          <NTLMAuthentication>False</NTLMAuthentication>
          <UseCustomServer>False</UseCustomServer>
          <CustomServerUrl>
          </CustomServerUrl>
          <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
        </WebProjectProperties>
      </FlavorProperties>
    </VisualStudio>
  </ProjectExtensions>
</Project>";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProductVersion>9.0.30729</ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{C7234361-F078-473B-BCA0-2E62A6DE4D46}</ProjectGuid>
    <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{F184B08F-C81C-45F6-A57F-5ABD9991F28F}</ProjectTypeGuids>
    <OutputType>Library</OutputType>
    <RootNamespace>Vs2008Sp1_Wap_35_Vb</RootNamespace>
    <AssemblyName>Vs2008Sp1_Wap_35_Vb</AssemblyName>
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <MyType>Custom</MyType>
    <OptionExplicit>On</OptionExplicit>
    <OptionCompare>Binary</OptionCompare>
    <OptionStrict>Off</OptionStrict>
    <OptionInfer>On</OptionInfer>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <DefineDebug>true</DefineDebug>
    <DefineTrace>true</DefineTrace>
    <OutputPath>bin\</OutputPath>
    <DocumentationFile>Vs2008Sp1_Wap_35_Vb.xml</DocumentationFile>
    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022,42353,42354,42355</NoWarn>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <DefineDebug>false</DefineDebug>
    <DefineTrace>true</DefineTrace>
    <Optimize>true</Optimize>
    <OutputPath>bin\</OutputPath>
    <DocumentationFile>Vs2008Sp1_Wap_35_Vb.xml</DocumentationFile>
    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022,42353,42354,42355</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Drawing"" />
    <Reference Include=""System.Core"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Data.DataSetExtensions"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Web.Extensions"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Xml.Linq"">
      <RequiredTargetFramework>3.5</RequiredTargetFramework>
    </Reference>
    <Reference Include=""System.Web"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""System.Configuration"" />
    <Reference Include=""System.Web.Services"" />
    <Reference Include=""System.EnterpriseServices"" />
    <Reference Include=""System.Web.Mobile"" />
  </ItemGroup>
  <ItemGroup>
    <Import Include=""Microsoft.VisualBasic"" />
    <Import Include=""System"" />
    <Import Include=""System.Collections"" />
    <Import Include=""System.Collections.Generic"" />
    <Import Include=""System.Data"" />
    <Import Include=""System.Linq"" />
    <Import Include=""System.Xml.Linq"" />
    <Import Include=""System.Diagnostics"" />
    <Import Include=""System.Collections.Specialized"" />
    <Import Include=""System.Configuration"" />
    <Import Include=""System.Text"" />
    <Import Include=""System.Text.RegularExpressions"" />
    <Import Include=""System.Web"" />
    <Import Include=""System.Web.Caching"" />
    <Import Include=""System.Web.SessionState"" />
    <Import Include=""System.Web.Security"" />
    <Import Include=""System.Web.Profile"" />
    <Import Include=""System.Web.UI"" />
    <Import Include=""System.Web.UI.WebControls"" />
    <Import Include=""System.Web.UI.WebControls.WebParts"" />
    <Import Include=""System.Web.UI.HtmlControls"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Default.aspx"" />
    <Content Include=""Web.config"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Default.aspx.vb"">
      <SubType>ASPXCodeBehind</SubType>
      <DependentUpon>Default.aspx</DependentUpon>
    </Compile>
    <Compile Include=""Default.aspx.designer.vb"">
      <DependentUpon>Default.aspx</DependentUpon>
    </Compile>
    <Compile Include=""My Project\AssemblyInfo.vb"" />
    <Compile Include=""My Project\Application.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DependentUpon>Application.myapp</DependentUpon>
    </Compile>
    <Compile Include=""My Project\MyExtensions\MyWebExtension.vb"">
      <VBMyExtensionTemplateID>Microsoft.VisualBasic.Web.MyExtension</VBMyExtensionTemplateID>
      <VBMyExtensionTemplateVersion>1.0.0.0</VBMyExtensionTemplateVersion>
    </Compile>
    <Compile Include=""My Project\Resources.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>Resources.resx</DependentUpon>
    </Compile>
    <Compile Include=""My Project\Settings.Designer.vb"">
      <AutoGen>True</AutoGen>
      <DependentUpon>Settings.settings</DependentUpon>
      <DesignTimeSharedInput>True</DesignTimeSharedInput>
    </Compile>
    <Compile Include=""Site1.Master.designer.vb"">
      <DependentUpon>Site1.Master</DependentUpon>
    </Compile>
    <Compile Include=""Site1.Master.vb"">
      <DependentUpon>Site1.Master</DependentUpon>
      <SubType>ASPXCodeBehind</SubType>
    </Compile>
    <Compile Include=""WebForm1.aspx.designer.vb"">
      <DependentUpon>WebForm1.aspx</DependentUpon>
    </Compile>
    <Compile Include=""WebForm1.aspx.vb"">
      <DependentUpon>WebForm1.aspx</DependentUpon>
      <SubType>ASPXCodebehind</SubType>
    </Compile>
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include=""My Project\Resources.resx"">
      <Generator>VbMyResourcesResXFileCodeGenerator</Generator>
      <LastGenOutput>Resources.Designer.vb</LastGenOutput>
      <CustomToolNamespace>My.Resources</CustomToolNamespace>
      <SubType>Designer</SubType>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup>
    <None Include=""My Project\Application.myapp"">
      <Generator>MyApplicationCodeGenerator</Generator>
      <LastGenOutput>Application.Designer.vb</LastGenOutput>
    </None>
    <None Include=""My Project\Settings.settings"">
      <Generator>SettingsSingleFileGenerator</Generator>
      <CustomToolNamespace>My</CustomToolNamespace>
      <LastGenOutput>Settings.Designer.vb</LastGenOutput>
    </None>
    <Content Include=""Site1.Master"" />
    <Content Include=""WebForm1.aspx"" />
  </ItemGroup>
  <ItemGroup>
    <Folder Include=""App_Data\"" />
  </ItemGroup>
  <PropertyGroup>
    <VisualStudioVersion Condition=""'$(VisualStudioVersion)' == ''"">10.0</VisualStudioVersion>
    <VSToolsPath Condition=""'$(VSToolsPath)' == ''"">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
  </PropertyGroup>
  <Import Project=""$(MSBuildBinPath)\Microsoft.VisualBasic.targets"" />
  <Import Project=""$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets"" Condition=""'$(VSToolsPath)' != ''"" />
  <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" Condition=""false"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
  <ProjectExtensions>
    <VisualStudio>
      <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
        <WebProjectProperties>
          <UseIIS>False</UseIIS>
          <AutoAssignPort>True</AutoAssignPort>
          <DevelopmentServerPort>64194</DevelopmentServerPort>
          <DevelopmentServerVPath>/</DevelopmentServerVPath>
          <IISUrl>
          </IISUrl>
          <NTLMAuthentication>False</NTLMAuthentication>
          <UseCustomServer>False</UseCustomServer>
          <CustomServerUrl>
          </CustomServerUrl>
          <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
        </WebProjectProperties>
      </FlavorProperties>
    </VisualStudio>
  </ProjectExtensions>
</Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// A VS2010 C# project file which requires Repair
        /// </summary>
        [TestMethod]
        public void ConvertCSharp2010RepairRequired()
        {
            string ProjectBefore = @"
                    <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                      <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                        <ProductVersion>
                        </ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{F4206430-F95D-4E52-B394-2E9E91EB362E}</ProjectGuid>
                        <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
                        <OutputType>Library</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Dev10Solution_Dev10RepairRequired</RootNamespace>
                        <AssemblyName>Dev10Solution_Dev10RepairRequired</AssemblyName>
                        <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include=""Microsoft.CSharp"" />
                        <Reference Include=""System"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Core"" />
                        <Reference Include=""System.Data.DataSetExtensions"" />
                        <Reference Include=""System.Web.Extensions"" />
                        <Reference Include=""System.Xml.Linq"" />
                        <Reference Include=""System.Drawing"" />
                        <Reference Include=""System.Web"" />
                        <Reference Include=""System.Xml"" />
                        <Reference Include=""System.Configuration"" />
                        <Reference Include=""System.Web.Services"" />
                        <Reference Include=""System.EnterpriseServices"" />
                        <Reference Include=""System.Web.DynamicData"" />
                        <Reference Include=""System.Web.Entity"" />
                        <Reference Include=""System.Web.ApplicationServices"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Content Include=""About.aspx"" />
                        <Content Include=""Account\ChangePassword.aspx"" />
                        <Content Include=""Account\ChangePasswordSuccess.aspx"" />
                        <Content Include=""Account\Login.aspx"" />
                        <Content Include=""Account\Register.aspx"" />
                        <Content Include=""Styles\Site.css"" />
                        <Content Include=""Default.aspx"" />
                        <Content Include=""Global.asax"" />
                        <Content Include=""Scripts\jquery-1.4.1-vsdoc.js"" />
                        <Content Include=""Scripts\jquery-1.4.1.js"" />
                        <Content Include=""Scripts\jquery-1.4.1.min.js"" />
                        <Content Include=""Web.config"" />
                        <Content Include=""Web.Debug.config"">
                          <DependentUpon>Web.config</DependentUpon>
                        </Content>
                        <Content Include=""Web.Release.config"">
                          <DependentUpon>Web.config</DependentUpon>
                        </Content>
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""About.aspx.cs"">
                          <DependentUpon>About.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""About.aspx.designer.cs"">
                          <DependentUpon>About.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Account\ChangePassword.aspx.cs"">
                          <DependentUpon>ChangePassword.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Account\ChangePassword.aspx.designer.cs"">
                          <DependentUpon>ChangePassword.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Account\ChangePasswordSuccess.aspx.cs"">
                          <DependentUpon>ChangePasswordSuccess.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Account\ChangePasswordSuccess.aspx.designer.cs"">
                          <DependentUpon>ChangePasswordSuccess.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Account\Login.aspx.cs"">
                          <DependentUpon>Login.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Account\Login.aspx.designer.cs"">
                          <DependentUpon>Login.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Account\Register.aspx.cs"">
                          <DependentUpon>Register.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Account\Register.aspx.designer.cs"">
                          <DependentUpon>Register.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Default.aspx.cs"">
                          <DependentUpon>Default.aspx</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Default.aspx.designer.cs"">
                          <DependentUpon>Default.aspx</DependentUpon>
                        </Compile>
                        <Compile Include=""Global.asax.cs"">
                          <DependentUpon>Global.asax</DependentUpon>
                        </Compile>
                        <Compile Include=""Properties\AssemblyInfo.cs"" />
                        <Compile Include=""Site.Master.cs"">
                          <DependentUpon>Site.Master</DependentUpon>
                          <SubType>ASPXCodeBehind</SubType>
                        </Compile>
                        <Compile Include=""Site.Master.designer.cs"">
                          <DependentUpon>Site.Master</DependentUpon>
                        </Compile>
                      </ItemGroup>
                      <ItemGroup>
                        <Folder Include=""App_Data\"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Content Include=""Account\Web.config"" />
                        <Content Include=""Site.Master"" />
                      </ItemGroup>
                      <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                      <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" />
                      <ProjectExtensions>
                        <VisualStudio>
                          <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
                            <WebProjectProperties>
                              <UseIIS>False</UseIIS>
                              <AutoAssignPort>True</AutoAssignPort>
                              <DevelopmentServerPort>63975</DevelopmentServerPort>
                              <DevelopmentServerVPath>/</DevelopmentServerVPath>
                              <IISUrl>
                              </IISUrl>
                              <NTLMAuthentication>False</NTLMAuthentication>
                              <UseCustomServer>False</UseCustomServer>
                              <CustomServerUrl>
                              </CustomServerUrl>
                              <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
                            </WebProjectProperties>
                          </FlavorProperties>
                        </VisualStudio>
                      </ProjectExtensions>
                    </Project>
            ";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
                        <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                          <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                          <PropertyGroup>
                            <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                            <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                            <ProductVersion>
                            </ProductVersion>
                            <SchemaVersion>2.0</SchemaVersion>
                            <ProjectGuid>{F4206430-F95D-4E52-B394-2E9E91EB362E}</ProjectGuid>
                            <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
                            <OutputType>Library</OutputType>
                            <AppDesignerFolder>Properties</AppDesignerFolder>
                            <RootNamespace>Dev10Solution_Dev10RepairRequired</RootNamespace>
                            <AssemblyName>Dev10Solution_Dev10RepairRequired</AssemblyName>
                            <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                          </PropertyGroup>
                          <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                            <DebugSymbols>true</DebugSymbols>
                            <DebugType>full</DebugType>
                            <Optimize>false</Optimize>
                            <OutputPath>bin\</OutputPath>
                            <DefineConstants>DEBUG;TRACE</DefineConstants>
                            <ErrorReport>prompt</ErrorReport>
                            <WarningLevel>4</WarningLevel>
                          </PropertyGroup>
                          <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                            <DebugType>pdbonly</DebugType>
                            <Optimize>true</Optimize>
                            <OutputPath>bin\</OutputPath>
                            <DefineConstants>TRACE</DefineConstants>
                            <ErrorReport>prompt</ErrorReport>
                            <WarningLevel>4</WarningLevel>
                          </PropertyGroup>
                          <ItemGroup>
                            <Reference Include=""Microsoft.CSharp"" />
                            <Reference Include=""System"" />
                            <Reference Include=""System.Data"" />
                            <Reference Include=""System.Core"" />
                            <Reference Include=""System.Data.DataSetExtensions"" />
                            <Reference Include=""System.Web.Extensions"" />
                            <Reference Include=""System.Xml.Linq"" />
                            <Reference Include=""System.Drawing"" />
                            <Reference Include=""System.Web"" />
                            <Reference Include=""System.Xml"" />
                            <Reference Include=""System.Configuration"" />
                            <Reference Include=""System.Web.Services"" />
                            <Reference Include=""System.EnterpriseServices"" />
                            <Reference Include=""System.Web.DynamicData"" />
                            <Reference Include=""System.Web.Entity"" />
                            <Reference Include=""System.Web.ApplicationServices"" />
                          </ItemGroup>
                          <ItemGroup>
                            <Content Include=""About.aspx"" />
                            <Content Include=""Account\ChangePassword.aspx"" />
                            <Content Include=""Account\ChangePasswordSuccess.aspx"" />
                            <Content Include=""Account\Login.aspx"" />
                            <Content Include=""Account\Register.aspx"" />
                            <Content Include=""Styles\Site.css"" />
                            <Content Include=""Default.aspx"" />
                            <Content Include=""Global.asax"" />
                            <Content Include=""Scripts\jquery-1.4.1-vsdoc.js"" />
                            <Content Include=""Scripts\jquery-1.4.1.js"" />
                            <Content Include=""Scripts\jquery-1.4.1.min.js"" />
                            <Content Include=""Web.config"" />
                            <Content Include=""Web.Debug.config"">
                              <DependentUpon>Web.config</DependentUpon>
                            </Content>
                            <Content Include=""Web.Release.config"">
                              <DependentUpon>Web.config</DependentUpon>
                            </Content>
                          </ItemGroup>
                          <ItemGroup>
                            <Compile Include=""About.aspx.cs"">
                              <DependentUpon>About.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""About.aspx.designer.cs"">
                              <DependentUpon>About.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Account\ChangePassword.aspx.cs"">
                              <DependentUpon>ChangePassword.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Account\ChangePassword.aspx.designer.cs"">
                              <DependentUpon>ChangePassword.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Account\ChangePasswordSuccess.aspx.cs"">
                              <DependentUpon>ChangePasswordSuccess.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Account\ChangePasswordSuccess.aspx.designer.cs"">
                              <DependentUpon>ChangePasswordSuccess.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Account\Login.aspx.cs"">
                              <DependentUpon>Login.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Account\Login.aspx.designer.cs"">
                              <DependentUpon>Login.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Account\Register.aspx.cs"">
                              <DependentUpon>Register.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Account\Register.aspx.designer.cs"">
                              <DependentUpon>Register.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Default.aspx.cs"">
                              <DependentUpon>Default.aspx</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Default.aspx.designer.cs"">
                              <DependentUpon>Default.aspx</DependentUpon>
                            </Compile>
                            <Compile Include=""Global.asax.cs"">
                              <DependentUpon>Global.asax</DependentUpon>
                            </Compile>
                            <Compile Include=""Properties\AssemblyInfo.cs"" />
                            <Compile Include=""Site.Master.cs"">
                              <DependentUpon>Site.Master</DependentUpon>
                              <SubType>ASPXCodeBehind</SubType>
                            </Compile>
                            <Compile Include=""Site.Master.designer.cs"">
                              <DependentUpon>Site.Master</DependentUpon>
                            </Compile>
                          </ItemGroup>
                          <ItemGroup>
                            <Folder Include=""App_Data\"" />
                          </ItemGroup>
                          <ItemGroup>
                            <Content Include=""Account\Web.config"" />
                            <Content Include=""Site.Master"" />
                          </ItemGroup>
                          <PropertyGroup>
                            <VisualStudioVersion Condition=""'$(VisualStudioVersion)' == ''"">10.0</VisualStudioVersion>
                            <VSToolsPath Condition=""'$(VSToolsPath)' == ''"">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
                          </PropertyGroup>
                          <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                          <Import Project=""$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets"" Condition=""'$(VSToolsPath)' != ''"" />
                          <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" Condition=""false"" />
                          <ProjectExtensions>
                            <VisualStudio>
                              <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
                                <WebProjectProperties>
                                  <UseIIS>False</UseIIS>
                                  <AutoAssignPort>True</AutoAssignPort>
                                  <DevelopmentServerPort>63975</DevelopmentServerPort>
                                  <DevelopmentServerVPath>/</DevelopmentServerVPath>
                                  <IISUrl>
                                  </IISUrl>
                                  <NTLMAuthentication>False</NTLMAuthentication>
                                  <UseCustomServer>False</UseCustomServer>
                                  <CustomServerUrl>
                                  </CustomServerUrl>
                                  <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
                                </WebProjectProperties>
                              </FlavorProperties>
                            </VisualStudio>
                          </ProjectExtensions>
                        </Project>
            ");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// A VS2010 Deploy project file which requires is no longer supported
        /// </summary>
        [TestMethod]
        public void VS2010DeployProjectDeprecated()
        {
            string ProjectBefore = @"
                    ""DeployProject""
                    {
                    ""VSVersion"" = ""3:800""
                    ""ProjectType"" = ""8:{978C614F-708E-4E1A-B201-565925725DBA}""
                    ""IsWebType"" = ""8:FALSE""
                    ""ProjectName"" = ""8:Dev10Dep1""
                    ""LanguageId"" = ""3:1033""
                    ""CodePage"" = ""3:1252""
                    ""UILanguageId"" = ""3:1033""
                    ""SccProjectName"" = ""8:""
                    ""SccLocalPath"" = ""8:""
                    ""SccAuxPath"" = ""8:""
                    ""SccProvider"" = ""8:""
                        ""Hierarchy""
                        {
                        }
                        ""Configurations""
                        {
                            ""Debug""
                            {
                            ""DisplayName"" = ""8:Debug""
                            ""IsDebugOnly"" = ""11:TRUE""
                            ""IsReleaseOnly"" = ""11:FALSE""
                            ""OutputFilename"" = ""8:Debug\\Dev10Dep1.msi""
                            ""PackageFilesAs"" = ""3:2""
                            ""PackageFileSize"" = ""3:-2147483648""
                            ""CabType"" = ""3:1""
                            ""Compression"" = ""3:2""
                            ""SignOutput"" = ""11:FALSE""
                            ""CertificateFile"" = ""8:""
                            ""PrivateKeyFile"" = ""8:""
                            ""TimeStampServer"" = ""8:""
                            ""InstallerBootstrapper"" = ""3:2""
                            }
                            ""Release""
                            {
                            ""DisplayName"" = ""8:Release""
                            ""IsDebugOnly"" = ""11:FALSE""
                            ""IsReleaseOnly"" = ""11:TRUE""
                            ""OutputFilename"" = ""8:Release\\Dev10Dep1.msi""
                            ""PackageFilesAs"" = ""3:2""
                            ""PackageFileSize"" = ""3:-2147483648""
                            ""CabType"" = ""3:1""
                            ""Compression"" = ""3:2""
                            ""SignOutput"" = ""11:FALSE""
                            ""CertificateFile"" = ""8:""
                            ""PrivateKeyFile"" = ""8:""
                            ""TimeStampServer"" = ""8:""
                            ""InstallerBootstrapper"" = ""3:2""
                            }
                        }
                        ""Deployable""
                        {
                            ""CustomAction""
                            {
                            }
                            ""DefaultFeature""
                            {
                            ""Name"" = ""8:DefaultFeature""
                            ""Title"" = ""8:""
                            ""Description"" = ""8:""
                            }
                            ""ExternalPersistence""
                            {
                                ""LaunchCondition""
                                {
                                }
                            }
                            ""File""
                            {
                            }
                            ""FileType""
                            {
                            }
                            ""Folder""
                            {
                                ""{1525181F-901A-416C-8A58-119130FE478E}:_0AA7A53E511C4817B4948E95C0DF4664""
                                {
                                ""Name"" = ""8:#1919""
                                ""AlwaysCreate"" = ""11:FALSE""
                                ""Condition"" = ""8:""
                                ""Transitive"" = ""11:FALSE""
                                ""Property"" = ""8:ProgramMenuFolder""
                                    ""Folders""
                                    {
                                    }
                                }
                                ""{3C67513D-01DD-4637-8A68-80971EB9504F}:_1B395C1DD72D471891B8440A6A066FE5""
                                {
                                ""DefaultLocation"" = ""8:[ProgramFilesFolder][Manufacturer]\\[ProductName]""
                                ""Name"" = ""8:#1925""
                                ""AlwaysCreate"" = ""11:FALSE""
                                ""Condition"" = ""8:""
                                ""Transitive"" = ""11:FALSE""
                                ""Property"" = ""8:TARGETDIR""
                                    ""Folders""
                                    {
                                    }
                                }
                                ""{1525181F-901A-416C-8A58-119130FE478E}:_D6EBF35E5CC343C290FAE28008DC6E7E""
                                {
                                ""Name"" = ""8:#1916""
                                ""AlwaysCreate"" = ""11:FALSE""
                                ""Condition"" = ""8:""
                                ""Transitive"" = ""11:FALSE""
                                ""Property"" = ""8:DesktopFolder""
                                    ""Folders""
                                    {
                                    }
                                }
                            }
                            ""LaunchCondition""
                            {
                            }
                            ""Locator""
                            {
                            }
                            ""MsiBootstrapper""
                            {
                            ""LangId"" = ""3:1033""
                            ""RequiresElevation"" = ""11:FALSE""
                            }
                            ""Product""
                            {
                            ""Name"" = ""8:Microsoft Visual Studio""
                            ""ProductName"" = ""8:Dev10Dep1""
                            ""ProductCode"" = ""8:{43FB18EF-67AC-473C-AD2A-DC07B7ABF16F}""
                            ""PackageCode"" = ""8:{4C46BDB8-9AB2-4369-83CF-203DF67A3561}""
                            ""UpgradeCode"" = ""8:{730CFD38-8449-4AFE-AED2-B34234601C56}""
                            ""AspNetVersion"" = ""8:4.0.30319.0""
                            ""RestartWWWService"" = ""11:FALSE""
                            ""RemovePreviousVersions"" = ""11:FALSE""
                            ""DetectNewerInstalledVersion"" = ""11:TRUE""
                            ""InstallAllUsers"" = ""11:FALSE""
                            ""ProductVersion"" = ""8:1.0.0""
                            ""Manufacturer"" = ""8:Microsoft""
                            ""ARPHELPTELEPHONE"" = ""8:""
                            ""ARPHELPLINK"" = ""8:""
                            ""Title"" = ""8:Dev10Dep1""
                            ""Subject"" = ""8:""
                            ""ARPCONTACT"" = ""8:Microsoft""
                            ""Keywords"" = ""8:""
                            ""ARPCOMMENTS"" = ""8:""
                            ""ARPURLINFOABOUT"" = ""8:""
                            ""ARPPRODUCTICON"" = ""8:""
                            ""ARPIconIndex"" = ""3:0""
                            ""SearchPath"" = ""8:""
                            ""UseSystemSearchPath"" = ""11:TRUE""
                            ""TargetPlatform"" = ""3:0""
                            ""PreBuildEvent"" = ""8:""
                            ""PostBuildEvent"" = ""8:""
                            ""RunPostBuildEvent"" = ""3:0""
                            }
                            ""Registry""
                            {
                                ""HKLM""
                                {
                                    ""Keys""
                                    {
                                        ""{60EA8692-D2D5-43EB-80DC-7906BF13D6EF}:_AC44CB3A56164C5FB678594056AC8A01""
                                        {
                                        ""Name"" = ""8:Software""
                                        ""Condition"" = ""8:""
                                        ""AlwaysCreate"" = ""11:FALSE""
                                        ""DeleteAtUninstall"" = ""11:FALSE""
                                        ""Transitive"" = ""11:FALSE""
                                            ""Keys""
                                            {
                                                ""{60EA8692-D2D5-43EB-80DC-7906BF13D6EF}:_8703E97B5EFC4DFB87EDC467D0E61560""
                                                {
                                                ""Name"" = ""8:[Manufacturer]""
                                                ""Condition"" = ""8:""
                                                ""AlwaysCreate"" = ""11:FALSE""
                                                ""DeleteAtUninstall"" = ""11:FALSE""
                                                ""Transitive"" = ""11:FALSE""
                                                    ""Keys""
                                                    {
                                                    }
                                                    ""Values""
                                                    {
                                                    }
                                                }
                                            }
                                            ""Values""
                                            {
                                            }
                                        }
                                    }
                                }
                                ""HKCU""
                                {
                                    ""Keys""
                                    {
                                        ""{60EA8692-D2D5-43EB-80DC-7906BF13D6EF}:_5BD957DCAAEA4AE49C46557E3D4B4076""
                                        {
                                        ""Name"" = ""8:Software""
                                        ""Condition"" = ""8:""
                                        ""AlwaysCreate"" = ""11:FALSE""
                                        ""DeleteAtUninstall"" = ""11:FALSE""
                                        ""Transitive"" = ""11:FALSE""
                                            ""Keys""
                                            {
                                                ""{60EA8692-D2D5-43EB-80DC-7906BF13D6EF}:_C51959C240B141E4B5DC7F3CB358EB03""
                                                {
                                                ""Name"" = ""8:[Manufacturer]""
                                                ""Condition"" = ""8:""
                                                ""AlwaysCreate"" = ""11:FALSE""
                                                ""DeleteAtUninstall"" = ""11:FALSE""
                                                ""Transitive"" = ""11:FALSE""
                                                    ""Keys""
                                                    {
                                                    }
                                                    ""Values""
                                                    {
                                                    }
                                                }
                                            }
                                            ""Values""
                                            {
                                            }
                                        }
                                    }
                                }
                                ""HKCR""
                                {
                                    ""Keys""
                                    {
                                    }
                                }
                                ""HKU""
                                {
                                    ""Keys""
                                    {
                                    }
                                }
                                ""HKPU""
                                {
                                    ""Keys""
                                    {
                                    }
                                }
                            }
                            ""Sequences""
                            {
                            }
                            ""Shortcut""
                            {
                            }
                            ""UserInterface""
                            {
                                ""{2479F3F5-0309-486D-8047-8187E2CE5BA0}:_1D4A4CE12E704A68BF617F9EBBD6DE62""
                                {
                                ""UseDynamicProperties"" = ""11:FALSE""
                                ""IsDependency"" = ""11:FALSE""
                                ""SourcePath"" = ""8:<VsdDialogDir>\\VsdBasicDialogs.wim""
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_3E838CB7B9B14D41B3A0BC24E5D1A219""
                                {
                                ""Name"" = ""8:#1901""
                                ""Sequence"" = ""3:2""
                                ""Attributes"" = ""3:2""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_01573A9060964A069C78FD9E40D8706F""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Progress""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdAdminProgressDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""ShowProgress""
                                                {
                                                ""Name"" = ""8:ShowProgress""
                                                ""DisplayName"" = ""8:#1009""
                                                ""Description"" = ""8:#1109""
                                                ""Type"" = ""3:5""
                                                ""ContextData"" = ""8:1;True=1;False=0""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:0""
                                                ""Value"" = ""3:1""
                                                ""DefaultValue"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_490F3627B9214CCEA509B6126EBAA586""
                                {
                                ""Name"" = ""8:#1901""
                                ""Sequence"" = ""3:1""
                                ""Attributes"" = ""3:2""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_42E52D514DE041D58DEEE621DA18551D""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Progress""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdProgressDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""ShowProgress""
                                                {
                                                ""Name"" = ""8:ShowProgress""
                                                ""DisplayName"" = ""8:#1009""
                                                ""Description"" = ""8:#1109""
                                                ""Type"" = ""3:5""
                                                ""ContextData"" = ""8:1;True=1;False=0""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:0""
                                                ""Value"" = ""3:1""
                                                ""DefaultValue"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_79632A079AC34475B054FB01EE83F43E""
                                {
                                ""Name"" = ""8:#1902""
                                ""Sequence"" = ""3:1""
                                ""Attributes"" = ""3:3""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_65FEFF54A86446E0A331A5DAB110A495""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Finished""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdFinishedDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""UpdateText""
                                                {
                                                ""Name"" = ""8:UpdateText""
                                                ""DisplayName"" = ""8:#1058""
                                                ""Description"" = ""8:#1158""
                                                ""Type"" = ""3:15""
                                                ""ContextData"" = ""8:""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:1""
                                                ""Value"" = ""8:#1258""
                                                ""DefaultValue"" = ""8:#1258""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_920D0874C0714728A52B67391F321564""
                                {
                                ""Name"" = ""8:#1902""
                                ""Sequence"" = ""3:2""
                                ""Attributes"" = ""3:3""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_68C15B16DFB74BF895B444A1E7F2A2E5""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Finished""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdAdminFinishedDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_CA216A037F2D49CABBA5C5230D50CA17""
                                {
                                ""Name"" = ""8:#1900""
                                ""Sequence"" = ""3:2""
                                ""Attributes"" = ""3:1""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_60BE1685AB5C48A8974B528786607841""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Welcome""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdAdminWelcomeDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""CopyrightWarning""
                                                {
                                                ""Name"" = ""8:CopyrightWarning""
                                                ""DisplayName"" = ""8:#1002""
                                                ""Description"" = ""8:#1102""
                                                ""Type"" = ""3:3""
                                                ""ContextData"" = ""8:""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:1""
                                                ""Value"" = ""8:#1202""
                                                ""DefaultValue"" = ""8:#1202""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""Welcome""
                                                {
                                                ""Name"" = ""8:Welcome""
                                                ""DisplayName"" = ""8:#1003""
                                                ""Description"" = ""8:#1103""
                                                ""Type"" = ""3:3""
                                                ""ContextData"" = ""8:""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:1""
                                                ""Value"" = ""8:#1203""
                                                ""DefaultValue"" = ""8:#1203""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_C1CA957AC4194CAE8883139CCE319BD8""
                                        {
                                        ""Sequence"" = ""3:300""
                                        ""DisplayName"" = ""8:Confirm Installation""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdAdminConfirmDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_E4416625D6DF4973918800B4910BC20D""
                                        {
                                        ""Sequence"" = ""3:200""
                                        ""DisplayName"" = ""8:Installation Folder""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdAdminFolderDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{DF760B10-853B-4699-99F2-AFF7185B4A62}:_DA1AB83886AE4E3ABF481FD039BBC368""
                                {
                                ""Name"" = ""8:#1900""
                                ""Sequence"" = ""3:1""
                                ""Attributes"" = ""3:1""
                                    ""Dialogs""
                                    {
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_1A17192A8B1B42AD8834821D94C34422""
                                        {
                                        ""Sequence"" = ""3:100""
                                        ""DisplayName"" = ""8:Welcome""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdWelcomeDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""CopyrightWarning""
                                                {
                                                ""Name"" = ""8:CopyrightWarning""
                                                ""DisplayName"" = ""8:#1002""
                                                ""Description"" = ""8:#1102""
                                                ""Type"" = ""3:3""
                                                ""ContextData"" = ""8:""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:1""
                                                ""Value"" = ""8:#1202""
                                                ""DefaultValue"" = ""8:#1202""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""Welcome""
                                                {
                                                ""Name"" = ""8:Welcome""
                                                ""DisplayName"" = ""8:#1003""
                                                ""Description"" = ""8:#1103""
                                                ""Type"" = ""3:3""
                                                ""ContextData"" = ""8:""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:1""
                                                ""Value"" = ""8:#1203""
                                                ""DefaultValue"" = ""8:#1203""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_31F1ACFF32E94780B48F7ED10871CAEE""
                                        {
                                        ""Sequence"" = ""3:300""
                                        ""DisplayName"" = ""8:Confirm Installation""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdConfirmDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                        ""{688940B3-5CA9-4162-8DEE-2993FA9D8CBC}:_332BF9E1EFDA4D12BFD34DA8CC44F337""
                                        {
                                        ""Sequence"" = ""3:200""
                                        ""DisplayName"" = ""8:Installation Folder""
                                        ""UseDynamicProperties"" = ""11:TRUE""
                                        ""IsDependency"" = ""11:FALSE""
                                        ""SourcePath"" = ""8:<VsdDialogDir>\\VsdFolderDlg.wid""
                                            ""Properties""
                                            {
                                                ""BannerBitmap""
                                                {
                                                ""Name"" = ""8:BannerBitmap""
                                                ""DisplayName"" = ""8:#1001""
                                                ""Description"" = ""8:#1101""
                                                ""Type"" = ""3:8""
                                                ""ContextData"" = ""8:Bitmap""
                                                ""Attributes"" = ""3:4""
                                                ""Setting"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                                ""InstallAllUsersVisible""
                                                {
                                                ""Name"" = ""8:InstallAllUsersVisible""
                                                ""DisplayName"" = ""8:#1059""
                                                ""Description"" = ""8:#1159""
                                                ""Type"" = ""3:5""
                                                ""ContextData"" = ""8:1;True=1;False=0""
                                                ""Attributes"" = ""3:0""
                                                ""Setting"" = ""3:0""
                                                ""Value"" = ""3:1""
                                                ""DefaultValue"" = ""3:1""
                                                ""UsePlugInResources"" = ""11:TRUE""
                                                }
                                            }
                                        }
                                    }
                                }
                                ""{2479F3F5-0309-486D-8047-8187E2CE5BA0}:_DF8B5EA652E34B8EB3D051F927859B9B""
                                {
                                ""UseDynamicProperties"" = ""11:FALSE""
                                ""IsDependency"" = ""11:FALSE""
                                ""SourcePath"" = ""8:<VsdDialogDir>\\VsdUserInterface.wim""
                                }
                            }
                            ""MergeModule""
                            {
                            }
                            ""ProjectOutput""
                            {
                            }
                        }
                    }
            ";

            try
            {
                Helpers.ConvertAndCompare(ProjectBefore, ProjectBefore);
                Assert.Fail("There should have been a Microsoft.Build.Exceptions.InvalidProjectFileException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(Microsoft.Build.Exceptions.InvalidProjectFileException));
            }
        }


        /// <summary>
        /// A VS2010 C# project file which requires no changes Repair
        /// </summary>
        [TestMethod]
        public void ConvertCSharp2010NoChangeRequired()
        {
            string ProjectBefore = @"
                    <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                      <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
                        <ProductVersion>8.0.30703</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{D97016B8-9FB6-4D56-A90E-C3DFD38AC032}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Dev10SolutionDev10VanillaProject</RootNamespace>
                        <AssemblyName>Dev10SolutionDev10VanillaProject</AssemblyName>
                        <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                        <TargetFrameworkProfile>Client</TargetFrameworkProfile>
                        <FileAlignment>512</FileAlignment>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
                        <PlatformTarget>x86</PlatformTarget>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
                        <PlatformTarget>x86</PlatformTarget>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include=""System"" />
                        <Reference Include=""System.Core"" />
                        <Reference Include=""System.Xml.Linq"" />
                        <Reference Include=""System.Data.DataSetExtensions"" />
                        <Reference Include=""Microsoft.CSharp"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Xml"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""Program.cs"" />
                        <Compile Include=""Properties\AssemblyInfo.cs"" />
                      </ItemGroup>
                      <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                    </Project>
                ";

            Helpers.ConvertAndCompare(ProjectBefore, ProjectBefore);
        }

        /// <summary>
        /// A VS2003 C# project file which requires conversion
        /// </summary>
        [TestMethod]
        public void ConvertCSharp2003ConversionRequired()
        {
            string ProjectBefore = @"
                    <VisualStudioProject>
                        <CSHARP
                            ProjectType = ""Local""
                            ProductVersion = ""7.10.6030""
                            SchemaVersion = ""2.0""
                            ProjectGuid = ""{C455AB6F-933F-441D-ABF8-D2F3FDA8CDA2}""
                        >
                            <Build>
                                <Settings
                                    ApplicationIcon = """"
                                    AssemblyKeyContainerName = """"
                                    AssemblyName = ""ClassLibrary1""
                                    AssemblyOriginatorKeyFile = """"
                                    DefaultClientScript = ""JScript""
                                    DefaultHTMLPageLayout = ""Grid""
                                    DefaultTargetSchema = ""IE50""
                                    DelaySign = ""false""
                                    OutputType = ""Library""
                                    PreBuildEvent = """"
                                    PostBuildEvent = """"
                                    RootNamespace = ""ClassLibrary1""
                                    RunPostBuildEvent = ""OnBuildSuccess""
                                    StartupObject = """"
                                >
                                    <Config
                                        Name = ""Debug""
                                        AllowUnsafeBlocks = ""false""
                                        BaseAddress = ""285212672""
                                        CheckForOverflowUnderflow = ""false""
                                        ConfigurationOverrideFile = """"
                                        DefineConstants = ""DEBUG;TRACE""
                                        DocumentationFile = """"
                                        DebugSymbols = ""true""
                                        FileAlignment = ""4096""
                                        IncrementalBuild = ""false""
                                        NoStdLib = ""false""
                                        NoWarn = """"
                                        Optimize = ""false""
                                        OutputPath = ""bin\Debug\""
                                        RegisterForComInterop = ""false""
                                        RemoveIntegerChecks = ""false""
                                        TreatWarningsAsErrors = ""false""
                                        WarningLevel = ""4""
                                    />
                                    <Config
                                        Name = ""Release""
                                        AllowUnsafeBlocks = ""false""
                                        BaseAddress = ""285212672""
                                        CheckForOverflowUnderflow = ""false""
                                        ConfigurationOverrideFile = """"
                                        DefineConstants = ""TRACE""
                                        DocumentationFile = """"
                                        DebugSymbols = ""false""
                                        FileAlignment = ""4096""
                                        IncrementalBuild = ""false""
                                        NoStdLib = ""false""
                                        NoWarn = """"
                                        Optimize = ""true""
                                        OutputPath = ""bin\Release\""
                                        RegisterForComInterop = ""false""
                                        RemoveIntegerChecks = ""false""
                                        TreatWarningsAsErrors = ""false""
                                        WarningLevel = ""4""
                                    />
                                </Settings>
                                <References>
                                    <Reference
                                        Name = ""System""
                                        AssemblyName = ""System""
                                        HintPath = ""..\..\..\..\..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.dll""
                                    />
                                    <Reference
                                        Name = ""System.Data""
                                        AssemblyName = ""System.Data""
                                        HintPath = ""..\..\..\..\..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Data.dll""
                                    />
                                    <Reference
                                        Name = ""System.XML""
                                        AssemblyName = ""System.XML""
                                        HintPath = ""..\..\..\..\..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.XML.dll""
                                    />
                                </References>
                            </Build>
                            <Files>
                                <Include>
                                    <File
                                        RelPath = ""AssemblyInfo.cs""
                                        SubType = ""Code""
                                        BuildAction = ""Compile""
                                    />
                                    <File
                                        RelPath = ""Class1.cs""
                                        SubType = ""Code""
                                        BuildAction = ""Compile""
                                    />
                                </Include>
                            </Files>
                        </CSHARP>
                    </VisualStudioProject>
                ";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
                        <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" DefaultTargets=""Build"">
                          <PropertyGroup>
                            <ProjectType>Local</ProjectType>
                            <ProductVersion>7.10.6030</ProductVersion>
                            <SchemaVersion>2.0</SchemaVersion>
                            <ProjectGuid>{C455AB6F-933F-441D-ABF8-D2F3FDA8CDA2}</ProjectGuid>
                            <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                            <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                            <ApplicationIcon />
                            <AssemblyKeyContainerName />
                            <AssemblyName>ClassLibrary1</AssemblyName>
                            <AssemblyOriginatorKeyFile />
                            <DefaultClientScript>JScript</DefaultClientScript>
                            <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
                            <DefaultTargetSchema>IE50</DefaultTargetSchema>
                            <DelaySign>false</DelaySign>
                            <OutputType>Library</OutputType>
                            <RootNamespace>ClassLibrary1</RootNamespace>
                            <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
                            <StartupObject />
                            <FileUpgradeFlags>20</FileUpgradeFlags>
                          </PropertyGroup>
                          <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                            <OutputPath>bin\Debug\</OutputPath>
                            <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                            <BaseAddress>285212672</BaseAddress>
                            <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                            <ConfigurationOverrideFile />
                            <DefineConstants>DEBUG;TRACE</DefineConstants>
                            <DocumentationFile />
                            <DebugSymbols>true</DebugSymbols>
                            <FileAlignment>4096</FileAlignment>
                            <NoStdLib>false</NoStdLib>
                            <NoWarn />
                            <Optimize>false</Optimize>
                            <RegisterForComInterop>false</RegisterForComInterop>
                            <RemoveIntegerChecks>false</RemoveIntegerChecks>
                            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                            <WarningLevel>4</WarningLevel>
                            <DebugType>full</DebugType>
                            <ErrorReport>prompt</ErrorReport>
                          </PropertyGroup>
                          <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                            <OutputPath>bin\Release\</OutputPath>
                            <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                            <BaseAddress>285212672</BaseAddress>
                            <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                            <ConfigurationOverrideFile />
                            <DefineConstants>TRACE</DefineConstants>
                            <DocumentationFile />
                            <DebugSymbols>false</DebugSymbols>
                            <FileAlignment>4096</FileAlignment>
                            <NoStdLib>false</NoStdLib>
                            <NoWarn />
                            <Optimize>true</Optimize>
                            <RegisterForComInterop>false</RegisterForComInterop>
                            <RemoveIntegerChecks>false</RemoveIntegerChecks>
                            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                            <WarningLevel>4</WarningLevel>
                            <DebugType>none</DebugType>
                            <ErrorReport>prompt</ErrorReport>
                          </PropertyGroup>
                          <ItemGroup>
                            <Reference Include=""System"">
                              <Name>System</Name>
                            </Reference>
                            <Reference Include=""System.Data"">
                              <Name>System.Data</Name>
                            </Reference>
                            <Reference Include=""System.XML"">
                              <Name>System.XML</Name>
                            </Reference>
                          </ItemGroup>
                          <ItemGroup>
                            <Compile Include=""AssemblyInfo.cs"">
                              <SubType>Code</SubType>
                            </Compile>
                            <Compile Include=""Class1.cs"">
                              <SubType>Code</SubType>
                            </Compile>
                          </ItemGroup>
                          <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                          <PropertyGroup>
                            <PreBuildEvent />
                            <PostBuildEvent />
                          </PropertyGroup>
                        </Project>
                    ");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// A VS2005 VB project file which requires conversion
        /// </summary>
        [TestMethod]
        public void ConvertVB2005ConversionRequired()
        {
            string ProjectBefore = @"
                    <Project DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                      <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                        <ProductVersion>8.0.50727</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{F23FE10A-EE6B-4800-8CFB-9CA07DA2D358}</ProjectGuid>
                        <OutputType>Library</OutputType>
                        <RootNamespace>Vs2005_ClassLib_20_Vb</RootNamespace>
                        <AssemblyName>Vs2005_ClassLib_20_Vb</AssemblyName>
                        <MyType>Windows</MyType>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <DefineDebug>true</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DocumentationFile>Vs2005_ClassLib_20_Vb.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                        <DebugType>pdbonly</DebugType>
                        <DefineDebug>false</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DocumentationFile>Vs2005_ClassLib_20_Vb.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include=""System"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Xml"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Import Include=""Microsoft.VisualBasic"" />
                        <Import Include=""System"" />
                        <Import Include=""System.Collections"" />
                        <Import Include=""System.Collections.Generic"" />
                        <Import Include=""System.Data"" />
                        <Import Include=""System.Diagnostics"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""Class1.vb"" />
                        <Compile Include=""My Project\AssemblyInfo.vb"" />
                        <Compile Include=""My Project\Application.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DependentUpon>Application.myapp</DependentUpon>
                        </Compile>
                        <Compile Include=""My Project\Resources.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DesignTime>True</DesignTime>
                          <DependentUpon>Resources.resx</DependentUpon>
                        </Compile>
                        <Compile Include=""My Project\Settings.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DependentUpon>Settings.settings</DependentUpon>
                          <DesignTimeSharedInput>True</DesignTimeSharedInput>
                        </Compile>
                      </ItemGroup>
                      <ItemGroup>
                        <EmbeddedResource Include=""My Project\Resources.resx"">
                          <Generator>VbMyResourcesResXFileCodeGenerator</Generator>
                          <LastGenOutput>Resources.Designer.vb</LastGenOutput>
                          <CustomToolNamespace>My.Resources</CustomToolNamespace>
                          <SubType>Designer</SubType>
                        </EmbeddedResource>
                      </ItemGroup>
                      <ItemGroup>
                        <None Include=""My Project\Application.myapp"">
                          <Generator>MyApplicationCodeGenerator</Generator>
                          <LastGenOutput>Application.Designer.vb</LastGenOutput>
                        </None>
                        <None Include=""My Project\Settings.settings"">
                          <Generator>SettingsSingleFileGenerator</Generator>
                          <CustomToolNamespace>My</CustomToolNamespace>
                          <LastGenOutput>Settings.Designer.vb</LastGenOutput>
                        </None>
                      </ItemGroup>
                    </Project>
                    ";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
                    <Project DefaultTargets=""Build"" xmlns=""msbuildnamespace"" ToolsVersion=""msbuilddefaulttoolsversion"">
                      <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                        <ProductVersion>8.0.50727</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{F23FE10A-EE6B-4800-8CFB-9CA07DA2D358}</ProjectGuid>
                        <OutputType>Library</OutputType>
                        <RootNamespace>Vs2005_ClassLib_20_Vb</RootNamespace>
                        <AssemblyName>Vs2005_ClassLib_20_Vb</AssemblyName>
                        <MyType>Windows</MyType>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <DefineDebug>true</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DocumentationFile>Vs2005_ClassLib_20_Vb.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                        <DebugType>pdbonly</DebugType>
                        <DefineDebug>false</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DocumentationFile>Vs2005_ClassLib_20_Vb.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include=""System"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Xml"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Import Include=""Microsoft.VisualBasic"" />
                        <Import Include=""System"" />
                        <Import Include=""System.Collections"" />
                        <Import Include=""System.Collections.Generic"" />
                        <Import Include=""System.Data"" />
                        <Import Include=""System.Diagnostics"" />
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""Class1.vb"" />
                        <Compile Include=""My Project\AssemblyInfo.vb"" />
                        <Compile Include=""My Project\Application.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DependentUpon>Application.myapp</DependentUpon>
                        </Compile>
                        <Compile Include=""My Project\Resources.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DesignTime>True</DesignTime>
                          <DependentUpon>Resources.resx</DependentUpon>
                        </Compile>
                        <Compile Include=""My Project\Settings.Designer.vb"">
                          <AutoGen>True</AutoGen>
                          <DependentUpon>Settings.settings</DependentUpon>
                          <DesignTimeSharedInput>True</DesignTimeSharedInput>
                        </Compile>
                      </ItemGroup>
                      <ItemGroup>
                        <EmbeddedResource Include=""My Project\Resources.resx"">
                          <Generator>VbMyResourcesResXFileCodeGenerator</Generator>
                          <LastGenOutput>Resources.Designer.vb</LastGenOutput>
                          <CustomToolNamespace>My.Resources</CustomToolNamespace>
                          <SubType>Designer</SubType>
                        </EmbeddedResource>
                      </ItemGroup>
                      <ItemGroup>
                        <None Include=""My Project\Application.myapp"">
                          <Generator>MyApplicationCodeGenerator</Generator>
                          <LastGenOutput>Application.Designer.vb</LastGenOutput>
                        </None>
                        <None Include=""My Project\Settings.settings"">
                          <Generator>SettingsSingleFileGenerator</Generator>
                          <CustomToolNamespace>My</CustomToolNamespace>
                          <LastGenOutput>Settings.Designer.vb</LastGenOutput>
                        </None>
                      </ItemGroup>
                    </Project>  
                    ");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// A VS2005 VB project file which requires conversion
        /// </summary>
        [TestMethod]
        public void ConvertCS2005ExcelProjectConversionRequired()
        {
            string projectBefore = 
                    @" <VisualStudioProject>
                        <CSHARP
                            ProjectType = ""Local""
                            ProductVersion = ""7.10.3077""
                            SchemaVersion = ""2.0""
                            ProjectGuid = ""{3CACC4D8-8DC1-418E-BB2C-3F9F60ECB2A5}""
                        >
                            <Build>
                                <Settings
                                    ApplicationIcon = """"
                                    AssemblyKeyContainerName = """"
                                    AssemblyName = ""BasicExcel""
                                    AssemblyOriginatorKeyFile = """"
                                    DefaultClientScript = ""JScript""
                                    DefaultHTMLPageLayout = ""Grid""
                                    DefaultTargetSchema = ""IE50""
                                    DelaySign = ""false""
                                    OutputType = ""Library""
                                    PreBuildEvent = """"
                                    PostBuildEvent = """"
                                    RootNamespace = ""BasicExcel""
                                    RunPostBuildEvent = ""OnBuildSuccess""
                                    StartupObject = """"
                                >
                                    <Config
                                        Name = ""Debug""
                                        AllowUnsafeBlocks = ""false""
                                        BaseAddress = ""285212672""
                                        CheckForOverflowUnderflow = ""false""
                                        ConfigurationOverrideFile = """"
                                        DefineConstants = ""DEBUG;TRACE""
                                        DocumentationFile = """"
                                        DebugSymbols = ""true""
                                        FileAlignment = ""4096""
                                        IncrementalBuild = ""false""
                                        NoStdLib = ""false""
                                        NoWarn = """"
                                        Optimize = ""false""
                                        OutputPath = ""bin\Debug\""
                                        RegisterForComInterop = ""false""
                                        RemoveIntegerChecks = ""false""
                                        TreatWarningsAsErrors = ""false""
                                        WarningLevel = ""4""
                                    />
                                    <Config
                                        Name = ""Release""
                                        AllowUnsafeBlocks = ""false""
                                        BaseAddress = ""285212672""
                                        CheckForOverflowUnderflow = ""false""
                                        ConfigurationOverrideFile = """"
                                        DefineConstants = ""TRACE""
                                        DocumentationFile = """"
                                        DebugSymbols = ""false""
                                        FileAlignment = ""4096""
                                        IncrementalBuild = ""false""
                                        NoStdLib = ""false""
                                        NoWarn = """"
                                        Optimize = ""true""
                                        OutputPath = ""bin\Release\""
                                        RegisterForComInterop = ""false""
                                        RemoveIntegerChecks = ""false""
                                        TreatWarningsAsErrors = ""false""
                                        WarningLevel = ""4""
                                    />
                                </Settings>
                                <References>
                                    <Reference
                                        Name = ""System""
                                        AssemblyName = ""System""
                                    />
                                    <Reference
                                        Name = ""System.Data""
                                        AssemblyName = ""System.Data""
                                    />
                                    <Reference
                                        Name = ""System.XML""
                                        AssemblyName = ""System.Xml""
                                    />
                                    <Reference
                                        Name = ""MSForms""
                                        Guid = ""{0D452EE1-E08F-101A-852E-02608C4D0BB4}""
                                        VersionMajor = ""2""
                                        VersionMinor = ""0""
                                        Lcid = ""0""
                                        WrapperTool = ""primary""
                                    />
                                    <Reference
                                        Name = ""System.Windows.Forms""
                                        AssemblyName = ""System.Windows.Forms""
                                        HintPath = ""..\..\..\..\..\..\..\WINDOWS\Microsoft.NET\Framework\v1.1.4322\System.Windows.Forms.dll""
                                    />
                                    <Reference
                                        Name = ""Microsoft.Office.Core""
                                        Guid = ""{2DF8D04C-5BFA-101B-BDE5-00AA0044DE52}""
                                        VersionMajor = ""2""
                                        VersionMinor = ""3""
                                        Lcid = ""0""
                                        WrapperTool = ""primary""
                                    />
                                    <Reference
                                        Name = ""VBIDE""
                                        Guid = ""{0002E157-0000-0000-C000-000000000046}""
                                        VersionMajor = ""5""
                                        VersionMinor = ""3""
                                        Lcid = ""0""
                                        WrapperTool = ""primary""
                                    />
                                    <Reference
                                        Name = ""Excel""
                                        Guid = ""{00020813-0000-0000-C000-000000000046}""
                                        VersionMajor = ""1""
                                        VersionMinor = ""5""
                                        Lcid = ""0""
                                        WrapperTool = ""primary""
                                    />
                                </References>
                            </Build>
                            <Files>
                                <Include>
                                    <File
                                        RelPath = ""AssemblyInfo.cs""
                                        SubType = ""Code""
                                        BuildAction = ""Compile""
                                    />
                                    <File
                                        RelPath = ""ThisWorkbook.cs""
                                        SubType = ""Code""
                                        BuildAction = ""Compile""
                                    />
                                </Include>
                            </Files>
                            <StartupServices>
                                <Service ID = ""{8F52F2DD-5E8A-4BBE-AFA6-5B941C11EED1}"" />
                            </StartupServices>
                            <UserProperties
                                OfficeDocumentPath = "".\BASICEXCEL.XLS""
                                OfficeProjectType = ""XLS""
                                OfficeProject = ""true""
                                TrustedAssembly = ""D:\WRProj\mdProjects\Excel\Document\CS\BasicExcel\BasicExcel\BasicExcel_bin\BasicExcel.dll""
                            />
                        </CSHARP>
                    </VisualStudioProject>
                    ";

            string projectAfter = ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" DefaultTargets=""Build"">
                      <PropertyGroup>
                        <ProjectType>Local</ProjectType>
                        <ProductVersion>7.10.3077</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{3CACC4D8-8DC1-418E-BB2C-3F9F60ECB2A5}</ProjectGuid>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                        <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                        <ApplicationIcon />
                        <AssemblyKeyContainerName />
                        <AssemblyName>BasicExcel</AssemblyName>
                        <AssemblyOriginatorKeyFile />
                        <DefaultClientScript>JScript</DefaultClientScript>
                        <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
                        <DefaultTargetSchema>IE50</DefaultTargetSchema>
                        <DelaySign>false</DelaySign>
                        <OutputType>Library</OutputType>
                        <RootNamespace>BasicExcel</RootNamespace>
                        <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
                        <StartupObject />
                        <ProjectTypeGuids>{BAA0C2D2-18E2-41B9-852F-F413020CAA33};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                        <FileUpgradeFlags>20</FileUpgradeFlags>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                        <OutputPath>bin\Debug\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>true</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>false</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>full</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                      </PropertyGroup>
                      <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                        <OutputPath>bin\Release\</OutputPath>
                        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
                        <BaseAddress>285212672</BaseAddress>
                        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
                        <ConfigurationOverrideFile />
                        <DefineConstants>TRACE</DefineConstants>
                        <DocumentationFile />
                        <DebugSymbols>false</DebugSymbols>
                        <FileAlignment>4096</FileAlignment>
                        <NoStdLib>false</NoStdLib>
                        <NoWarn />
                        <Optimize>true</Optimize>
                        <RegisterForComInterop>false</RegisterForComInterop>
                        <RemoveIntegerChecks>false</RemoveIntegerChecks>
                        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
                        <WarningLevel>4</WarningLevel>
                        <DebugType>none</DebugType>
                        <ErrorReport>prompt</ErrorReport>
                      </PropertyGroup>
                      <ItemGroup>
                        <COMReference Include=""Excel"">
                          <Guid>{00020813-0000-0000-C000-000000000046}</Guid>
                          <VersionMajor>1</VersionMajor>
                          <VersionMinor>5</VersionMinor>
                          <Lcid>0</Lcid>
                          <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=""Microsoft.Office.Core"">
                          <Guid>{2DF8D04C-5BFA-101B-BDE5-00AA0044DE52}</Guid>
                          <VersionMajor>2</VersionMajor>
                          <VersionMinor>3</VersionMinor>
                          <Lcid>0</Lcid>
                          <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=""MSForms"">
                          <Guid>{0D452EE1-E08F-101A-852E-02608C4D0BB4}</Guid>
                          <VersionMajor>2</VersionMajor>
                          <VersionMinor>0</VersionMinor>
                          <Lcid>0</Lcid>
                          <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <COMReference Include=""VBIDE"">
                          <Guid>{0002E157-0000-0000-C000-000000000046}</Guid>
                          <VersionMajor>5</VersionMajor>
                          <VersionMinor>3</VersionMinor>
                          <Lcid>0</Lcid>
                          <WrapperTool>primary</WrapperTool>
                        </COMReference>
                        <Reference Include=""System"">
                          <Name>System</Name>
                        </Reference>
                        <Reference Include=""System.Data"">
                          <Name>System.Data</Name>
                        </Reference>
                        <Reference Include=""System.Windows.Forms"">
                          <Name>System.Windows.Forms</Name>
                        </Reference>
                        <Reference Include=""System.Xml"">
                          <Name>System.XML</Name>
                        </Reference>
                      </ItemGroup>
                      <ItemGroup>
                        <Compile Include=""AssemblyInfo.cs"">
                          <SubType>Code</SubType>
                        </Compile>
                        <Compile Include=""ThisWorkbook.cs"">
                          <SubType>Code</SubType>
                        </Compile>
                      </ItemGroup>
                      <ItemGroup>
                        <Service Include=""{8F52F2DD-5E8A-4BBE-AFA6-5B941C11EED1}"" />
                      </ItemGroup>
                      <ProjectExtensions>
                        <VisualStudio>
                          <UserProperties OfficeDocumentPath="".\BASICEXCEL.XLS"" OfficeProjectType=""XLS"" OfficeProject=""true"" TrustedAssembly=""D:\WRProj\mdProjects\Excel\Document\CS\BasicExcel\BasicExcel\BasicExcel_bin\BasicExcel.dll"" />
                        </VisualStudio>
                      </ProjectExtensions>
                      <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                      <PropertyGroup>
                        <PreBuildEvent />
                        <PostBuildEvent />
                      </PropertyGroup>
                    </Project>
                    ")
                     ;

            Helpers.ConvertAndCompare(projectBefore, projectAfter);
        }

        /// <summary>
        /// Check that when we're upgrading projects referencing and compiling .xaml source files
        /// we are correctly appending Generator and Subtype properties to the source file and not the
        /// reference
        /// Check also that project references to vcproj have their extensions fixed to .vcxproj
        /// </summary>
        [TestMethod]
        public void CheckForReferencesReplacements()
        {
            string ProjectBefore = @"
            <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcproj "">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"" />
                <Compile Include=""MySampleFile2.xaml "" />
              </ItemGroup>
            </Project>";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                    <Generator>MSBuild:Compile</Generator>
                    <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                    <Generator>MSBuild:Compile</Generator>
                    <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// Check that when we're upgrading projects referencing and compiling .xaml source files
        /// we are correctly appending Generator and Subtype properties to the source file and not the
        /// reference
        /// 
        /// Check also that project references to vcproj have their extensions fixed to .vcxproj
        /// 
        /// Lastly, make sure that this still happens even if the ToolsVersion is > 3.5.
        /// </summary>
        [TestMethod]
        public void CheckForReferencesReplacements_NewerToolsVersion()
        {
            string ProjectBefore = @"
            <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcproj "">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"" />
                <Compile Include=""MySampleFile2.xaml "" />
              </ItemGroup>
            </Project>";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                    <Generator>MSBuild:Compile</Generator>
                    <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                    <Generator>MSBuild:Compile</Generator>
                    <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// Check that when we're upgrading projects referencing and compiling .xaml source files
        /// that we don't append the Generator and SubType properties to the source file if they 
        /// are already set.
        /// </summary>
        [TestMethod]
        public void DoNotReplacePreExistingXamlProperties()
        {
            string ProjectBefore = @"
            <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcproj "">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// Check that when we're upgrading projects referencing and compiling .xaml source files
        /// that even if there are multiple instances of the metadata, we don't eliminate them -- 
        /// we just also don't add any more. 
        /// </summary>
        [TestMethod]
        public void DontEliminateDuplicateXamlProperties()
        {
            string ProjectBefore = @"
            <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcproj "">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile3.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>";

            string ProjectAfter = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include=""SampeProject.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92d}</Project>
                  <Name>SampeProject</Name>
                </ProjectReference>
                <ProjectReference Include=""SampeProject2.vcxproj"">
                  <Project>{a289b3a9-e10c-4bb8-8814-18faf19ec92e}</Project>
                  <Name>SampeProject2</Name>
                </ProjectReference>
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile3.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfter);
        }

        /// <summary>
        /// Check that when we're upgrading projects referencing and compiling .xaml source files
        /// that already have Generator and Subtype metadata, we don't make any changes and 
        /// don't update the ToolsVersion. 
        /// </summary>
        [TestMethod]
        public void DontUpdateToolsVersionIfNothingChanged()
        {
            string ProjectBeforeAndAfter = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
              <ItemGroup>
                <Reference Include=""System.Xaml"" />
                <Reference Include=""System"" />
                <Reference Include=""System.Xml"" />
                <Reference Include=""SampleRef.Xaml "" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include=""MySampleFile.xaml"">
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile2.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Compile>
                <Compile Include=""MySampleFile3.xaml "">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                  <SubType>Designer</SubType>
                </Compile>
              </ItemGroup>
            </Project>");

            Helpers.ConvertAndCompare(ProjectBeforeAndAfter, ProjectBeforeAndAfter);
        }

        #endregion

        [TestMethod]
        public void MinorUpgradeShouldNotUpdateToolsVersion()
        {
            string ProjectBefore = @"
            <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" />
            </Project>";

            string ProjectAfterByDefault = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                <PropertyGroup>
                    <VisualStudioVersion Condition=""'$(VisualStudioVersion)' == ''"">10.0</VisualStudioVersion>
                    <VSToolsPath Condition=""'$(VSToolsPath)' == ''"">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
                </PropertyGroup>
                <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                <Import Project=""$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets"" Condition=""'$(VSToolsPath)' != ''"" />
                <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" Condition=""false"" />
            </Project>");

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfterByDefault);

            string ProjectAfterMinorUpgrade = @"
            <Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                <PropertyGroup>
                    <VisualStudioVersion Condition=""'$(VisualStudioVersion)' == ''"">10.0</VisualStudioVersion>
                    <VSToolsPath Condition=""'$(VSToolsPath)' == ''"">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
                </PropertyGroup>
                <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                <Import Project=""$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets"" Condition=""'$(VSToolsPath)' != ''"" />
                <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" Condition=""false"" />
            </Project>";

            Helpers.ConvertAndCompare(ProjectBefore, ProjectAfterMinorUpgrade, null, isMinorUpgrade: true);
        }
    }
}
