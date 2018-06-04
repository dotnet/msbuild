// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests from Orcas
    /// </summary>
    sealed public class TargetsFile_Test
    {
#if FEATURE_COMPILE_IN_TESTS
        /// <summary>
        /// Check that the ARM flag is passed to the compiler when targeting ARM.
        /// </summary>
        [Fact]
        public void TargetARM()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>arm</PlatformTarget>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:arm ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        #region 32bit preferred

        /// <summary>
        /// Check that with an empty platformtarget (equivalent to anycpu), library type assemblies do not 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void AnyCPULibraryProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogDoesntContain(" /platform:");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an explicit platform of anycpu, library type assemblies do not 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void ExplicitAnyCPULibraryProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:AnyCPU ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an empty platformtarget (equivalent to anycpu), winmdobj type assemblies do not 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void AnyCPUWinMDObjProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>winmdobj</OutputType>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <!-- For dealing with the case where the Jupiter targets do not exist, in order to follow the appropriate codepaths in the standard managed
                           we need to be .NET 4.5 or greater -->
                      <PropertyGroup Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')`>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets` Condition=`Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                      <!-- Fall back to CSharp targets for the sake of this test if the Jupiter targets don't exist, since what we're testing can be equally well resolved by either -->
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogDoesntContain(" /platform:");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an explicit platformtarget of anycpu, winmdobj type assemblies do not 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void ExplicitAnyCPUWinMDObjProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>winmdobj</OutputType>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                      </PropertyGroup>
                      <!-- For dealing with the case where the Jupiter targets do not exist, in order to follow the appropriate codepaths in the standard managed
                           we need to be .NET 4.5 or greater -->
                      <PropertyGroup Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')`>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets` Condition=`Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                      <!-- Fall back to CSharp targets for the sake of this test if the Jupiter targets don't exist, since what we're testing can be equally well resolved by either -->
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:AnyCPU ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an empty platformtarget (equivalent to anycpu), exe type assemblies 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void AnyCPUExeProjectIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an explicit platformtarget of anycpu, exe type assemblies 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void ExplicitAnyCPUExeProjectIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an empty platformtarget (equivalent to anycpu), exe type assemblies 
        /// that are targeting .NET 4.0 do not get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void AnyCPU40ExeProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogDoesntContain(" /platform:");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an explicit platformtarget of anycpu, exe type assemblies that are 
        /// targeting .NET 4.0 do not get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void ExplicitAnyCPU40ExeProjectIsNot32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:AnyCPU ");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an empty platformtarget (equivalent to anycpu), appcontainerexe type assemblies 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void AnyCPUAppContainerExeProjectIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>AppContainerExe</OutputType>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <!-- For dealing with the case where the Jupiter targets do not exist, in order to follow the appropriate codepaths in the standard managed
                           we need to be .NET 4.5 or greater -->
                      <PropertyGroup Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')`>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets` Condition=`Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                      <!-- Fall back to CSharp targets for the sake of this test if the Jupiter targets don't exist, since what we're testing can be equally well resolved by either -->
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Check that with an explicit platformtarget of anycpu, appcontainerexe type assemblies 
        /// get forced to anycpu32bitpreferred by default. 
        /// </summary>
        [Fact]
        public void ExplicitAnyCPUAppContainerExeProjectIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>AppContainerExe</OutputType>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                      </PropertyGroup>
                      <!-- For dealing with the case where the Jupiter targets do not exist, in order to follow the appropriate codepaths in the standard managed
                           we need to be .NET 4.5 or greater -->
                      <PropertyGroup Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')`>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets` Condition=`Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                      <!-- Fall back to CSharp targets for the sake of this test if the Jupiter targets don't exist, since what we're testing can be equally well resolved by either -->
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Although AnyCPU library projects should not default to AnyCPU32BitPreferred, because that platform is 
        /// not supported for library projects, if Prefer32Bit is explicitly set, we should still respect that. 
        /// </summary>
        [Fact]
        public void AnyCPULibraryProjectIs32BitPreferredIfPrefer32BitSet()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`15.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <Prefer32Bit>true</Prefer32Bit>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// A project with no explicit OutputType will end up defaulting its OutputType to exe, 
        /// so it should also default to Prefer32Bit = true. 
        /// </summary>
        [Fact]
        public void AnyCPUProjectWithNoExplicitOutputTypeIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// A project with no explicit OutputType will end up defaulting its OutputType to exe, 
        /// so it should also default to Prefer32Bit = true. 
        /// </summary>
        [Fact]
        public void AnyCPUJupiterProjectWithNoExplicitOutputTypeIs32BitPreferred()
        {
            string file = null;
            string outputPath = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                file = Helpers.CreateFiles("class1.cs")[0];

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <Configuration>Debug</Configuration>
                        <PlatformTarget>AnyCPU</PlatformTarget>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + file + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets` Condition=`Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                      <!-- Fall back to CSharp targets for the sake of this test if the Jupiter targets don't exist, since what we're testing can be equally well resolved by either -->
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` Condition=`!Exists('$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v1.0\Microsoft.Windows.UI.Xaml.CSharp.targets')` />
                   </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(" /platform:anycpu32bitpreferred");
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }
        #endregion 32bit preferred

        /// <summary>
        /// Validate that the GetFrameworkPaths target 
        /// </summary>
        [Fact]
        public void TestGetFrameworkPaths()
        {
            MockLogger logger = new MockLogger();

            Project project = ObjectModelHelpers.CreateInMemoryProject(
                @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` DefaultTargets=`GetPaths` xmlns=`msbuildnamespace`>
  <Target Name=`GetPaths` DependsOnTargets=`GetFrameworkPaths`>
    <Message Text=`Framework 4.0 = @(_TargetFramework40DirectoryItem)` Importance=`High`/>
    <Message Text=`Framework 3.5 = @(_TargetFramework35DirectoryItem)` Importance=`High`/>
    <Message Text=`Framework 3.0 = @(_TargetFramework30DirectoryItem)` Importance=`High`/>
    <Message Text=`Framework 2.0 = @(_TargetFramework20DirectoryItem)` Importance=`High`/>
  </Target>
  <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
</Project>",
             logger
             );

            project.Build();

            logger.AssertLogContains(false/* not case sensitive */, "Framework 4.0 = " + FrameworkLocationHelper.PathToDotNetFrameworkV40);

            // Only check .NET 3.5 and below if they're actually on the box.
            if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
            {
                logger.AssertLogContains(false/* not case sensitive */, "Framework 3.5 = " + FrameworkLocationHelper.PathToDotNetFrameworkV35);
                logger.AssertLogContains(false/* not case sensitive */, "Framework 3.0 = " + FrameworkLocationHelper.PathToDotNetFrameworkV30);
                logger.AssertLogContains(false/* not case sensitive */, "Framework 2.0 = " + FrameworkLocationHelper.PathToDotNetFrameworkV20);
            }
        }

        /// <summary>
        /// Validate that the GetFrameworkPaths target 
        /// </summary>
        [Fact]
        public void TestTargetFrameworkPaths()
        {
            string[] targetFrameworkVersions = { "v2.0", "v3.0", "v3.5", "v4.0", "v4.5", "" };
            foreach (var version in targetFrameworkVersions)
            {
                MockLogger logger = new MockLogger();
                string projString = ObjectModelHelpers.CleanupFileContents(
                    @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` DefaultTargets=`GetPaths` xmlns=`msbuildnamespace`>
  <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
  <Target
      Name='GetPaths'
      DependsOnTargets='GetFrameworkPaths'>
    <Message Text='Target Framework Folder = @(_TargetedFrameworkDirectoryItem)' />
  </Target>
</Project>"
                   );
                Project project = ObjectModelHelpers.CreateInMemoryProject(
                 projString,
                 logger
                 );

                project.SetProperty("TargetFrameworkVersion", version);
                project.Build();

                string targetFrameworkVersion = project.GetPropertyValue("TargetFrameworkVersion");
                string msbuildFrameworkToolsRoot = project.GetPropertyValue("MSBuildFrameworkToolsRoot");

                if (targetFrameworkVersion.Equals("v2.0"))
                {
                    if (FrameworkLocationHelper.PathToDotNetFrameworkV20 != null)
                    {
                        logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + FrameworkLocationHelper.PathToDotNetFrameworkV20);
                    }
                    else
                    {
                        // If Framework v2.0 isn't present we use the hard coded version for this validation
                        logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + msbuildFrameworkToolsRoot + "v2.0.50727");
                    }
                }
                else if (targetFrameworkVersion.Equals("v3.0") || targetFrameworkVersion.Equals("v3.5"))
                {
                    logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + msbuildFrameworkToolsRoot + "\\" + targetFrameworkVersion);
                }
                else if (targetFrameworkVersion.Equals("v4.0"))
                {
                    logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + FrameworkLocationHelper.PathToDotNetFrameworkV40);
                }
                else if (targetFrameworkVersion.Equals("v4.5"))
                {
                    logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + FrameworkLocationHelper.PathToDotNetFrameworkV45);
                }
                else if (String.IsNullOrEmpty(targetFrameworkVersion))
                {
                    logger.AssertLogContains(false/* not case sensitive */, "Target Framework Folder = " + FrameworkLocationHelper.PathToDotNetFrameworkV45);
                }
            }
        }

        #region AssignLinkMetadata targets tests

        /// <summary>
        /// Doesn't synthesize Link metadata if the items are defined in the project  
        /// </summary>
        [Fact]
        public void NoLinkMetadataSynthesisWhenDefinedInProject()
        {
            string[] files = null;
            string outputPath = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));

            try
            {
                files = Helpers.CreateFiles("class1.cs", "File1.txt", "Content1.foo");

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <SynthesizeLinkMetadata>true</SynthesizeLinkMetadata>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`" + files[0] + @"` />
                        <None Include=`" + files[1] + @"` />
                        <Content Include=`" + files[2] + @"` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                      <Target Name=`AfterBuild`>
                        <Message Text=`%(Compile.Identity): [%(Compile.Link)]` />
                        <Message Text=`%(None.Identity): [%(None.Link)]` />
                        <Message Text=`%(Content.Identity): [%(Content.Link)]` />
                      </Target>
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(String.Format("{0}: []", files[0]));
                logger.AssertLogContains(String.Format("{0}: []", files[1]));
                logger.AssertLogContains(String.Format("{0}: []", files[2]));
            }
            finally
            {
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Synthesizes Link metadata if the items are defined in an import and are on the whitelist
        /// </summary>
        [Fact]
        public void SynthesizeLinkMetadataForItemsOnWhitelist()
        {
            string[] files = null;
            string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string directoryToDelete = null;

            try
            {
                files = Helpers.CreateFiles("class1.cs", "File1.txt", "Content1.foo", "a.proj");

                directoryToDelete = Path.GetDirectoryName(files[0]);
                string subProjectDirectory = Path.Combine(Path.GetDirectoryName(files[0]), "SubFolder");
                Directory.CreateDirectory(subProjectDirectory);

                string classPath = Path.Combine(subProjectDirectory, "Class1.cs");
                string textFilePath = Path.Combine(subProjectDirectory, "File1.txt");
                string contentPath = Path.Combine(subProjectDirectory, "Content1.foo");

                File.Move(files[0], classPath);
                File.Move(files[1], textFilePath);
                File.Move(files[2], contentPath);

                string sharedFilesProjectContents =
                            @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                              <ItemGroup>
                                <Compile Include=`" + classPath + @"` />
                                <None Include=`" + textFilePath + @"` />
                                <Content Include=`" + contentPath + @"` />
                              </ItemGroup>
                           </Project>";

                File.WriteAllText(files[3], ObjectModelHelpers.CleanupFileContents(sharedFilesProjectContents));

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <SynthesizeLinkMetadata>true</SynthesizeLinkMetadata>
                      </PropertyGroup>
                      <Import Project=`" + files[3] + @"` />
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                      <Target Name=`AfterBuild`>
                        <Message Text=`%(Compile.Identity): [%(Compile.Link)]` />
                        <Message Text=`%(None.Identity): [%(None.Link)]` />
                        <Message Text=`%(Content.Identity): [%(Content.Link)]` />
                      </Target>
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(String.Format(@"{0}: []", classPath));
                logger.AssertLogContains(String.Format(@"{0}: [SubFolder" + Path.DirectorySeparatorChar + "File1.txt]", textFilePath));
                logger.AssertLogContains(String.Format(@"{0}: [SubFolder" + Path.DirectorySeparatorChar + "Content1.foo]", contentPath));
            }
            finally
            {
                if (directoryToDelete != null)
                {
                    ObjectModelHelpers.DeleteDirectory(directoryToDelete);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        /// <summary>
        /// Don't synthesize link metadata if the SynthesizeLinkMetadata property is false
        /// </summary>
        [Fact]
        public void DontSynthesizeLinkMetadataIfPropertyNotSet()
        {
            string[] files = null;
            string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string directoryToDelete = null;

            try
            {
                files = Helpers.CreateFiles("class1.cs", "File1.txt", "Content1.foo", "a.proj");

                directoryToDelete = Path.GetDirectoryName(files[0]);
                string subProjectDirectory = Path.Combine(Path.GetDirectoryName(files[0]), "SubFolder");
                Directory.CreateDirectory(subProjectDirectory);

                string classPath = Path.Combine(subProjectDirectory, "Class1.cs");
                string textFilePath = Path.Combine(subProjectDirectory, "File1.txt");
                string contentPath = Path.Combine(subProjectDirectory, "Content1.foo");

                File.Move(files[0], classPath);
                File.Move(files[1], textFilePath);
                File.Move(files[2], contentPath);

                string sharedFilesProjectContents =
                         @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                              <ItemGroup>
                                <Compile Include=`" + classPath + @"` />
                                <None Include=`" + textFilePath + @"` />
                                <Content Include=`" + contentPath + @"` />
                              </ItemGroup>
                           </Project>";

                File.WriteAllText(files[3], ObjectModelHelpers.CleanupFileContents(sharedFilesProjectContents));

                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(
                    @"
                   <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>" + outputPath + @"</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Library</OutputType>
                        <Configuration>Debug</Configuration>
                        <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                        <SynthesizeLinkMetadata>false</SynthesizeLinkMetadata>
                      </PropertyGroup>
                      <Import Project=`" + files[3] + @"` />
                      <Import Project=`$(MSBuildToolsPath)\Microsoft.CSharp.targets` />
                      <Target Name=`AfterBuild`>
                        <Message Text=`%(Compile.Identity): [%(Compile.Link)]` />
                        <Message Text=`%(None.Identity): [%(None.Link)]` />
                        <Message Text=`%(Content.Identity): [%(Content.Link)]` />
                      </Target>
                    </Project>
                ",
                 logger
                 );

                project.Build();

                logger.AssertLogContains(String.Format(@"{0}: []", classPath));
                logger.AssertLogContains(String.Format(@"{0}: []", textFilePath));
                logger.AssertLogContains(String.Format(@"{0}: []", contentPath));
            }
            finally
            {
                if (directoryToDelete != null)
                {
                    ObjectModelHelpers.DeleteDirectory(directoryToDelete);
                }

                ObjectModelHelpers.DeleteDirectory(outputPath);
            }
        }

        #endregion AssignLinkMetadata targets tests
#endif

#if _NOT_YET_FULLY_CONVERTED_
        /// <summary>
        /// Tests that exercise the SplitResourcesByCulture Target in Microsoft.Common.targets.
        /// This target's job is to separate the items that need to run through resgen from 
        /// those that need to go directly into CSC. Also, Culture and non-Culture resources 
        /// are split.
        /// </summary>
        [Test]
        public void SplitResourcesByCultureTarget()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                      <PropertyGroup>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`Class1.cs` />
                        <EmbeddedResource Include=`Resource1.txt` />
                        <EmbeddedResource Include=`Resource2.resx` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                    </Project>
                "); 

            p.Build(new string [] {"SplitResourcesByCulture"}, null);

            ProjectItem[] items = p.GetItems("EmbeddedResource").ToArray();                        

            Assert.AreEqual("Resource2.resx", items[0].EvaluatedInclude);
            Assert.AreEqual("false", items[0].GetMetadataValue("WithCulture"));
            Assert.AreEqual("Resx", items[0].GetMetadataValue("Type"));

            Assert.AreEqual("Resource1.txt", items[1].EvaluatedInclude);
            Assert.AreEqual("false", items[1].GetMetadataValue("WithCulture"));
            Assert.AreEqual("Non-Resx", items[1].GetMetadataValue("Type"));
        }

        /// <summary>
        /// Test to make sure that referenced projects are being cleaned properly.
        /// </summary>
        [Test]
        public void Regress565788()
        {
            Helper.CreateTempCSharpProjectWithClassLibrary();

            string[] buildTarget = new string[1];
            buildTarget[0] = "Build";
            MockLogger log = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", buildTarget, null);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ClassLibrary\bin\debug\ClassLibrary.dll",
                "Failed to create ClassLibrary.dll, which should have been created because there was P2P reference from ConsoleApplication");

            buildTarget[0] = "Clean";
            log = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", buildTarget, null);

            ObjectModelHelpers.AssertFileDoesNotExistInTempProjectDirectory(@"ClassLibrary\bin\debug\ClassLibrary.dll",
                "Failed to delete ClassLibrary.dll, which should have been deleted because there was P2P reference from ConsoleApplication");
        }

        /// <summary>
        /// Tests that we correctly handle .RESTEXT files marked as EmbeddedResource in the project.
        /// </summary>
        [Test]
        public void ResTextFiles_CSharp()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ----------------------------------------------------------------------------
            // ConsoleApplication37.csproj
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication37.csproj", @"

                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{1EE23632-5998-4CF5-9EAD-11FDC67456E6}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>ConsoleApplication37</RootNamespace>
                        <AssemblyName>ConsoleApplication37</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Program.cs` />
                    </ItemGroup>
                    <ItemGroup>
                        <EmbeddedResource Include=`Strings1.restext` />
                        <EmbeddedResource Include=`Strings2.restext`>
                            <LogicalName>MyStrings2.resources</LogicalName>
                        </EmbeddedResource>
                        <EmbeddedResource Include=`Subfolder\Strings3.restext` />
                    </ItemGroup>
                    <ItemGroup>
                        <Folder Include=`Properties\` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>

                ");

            // ----------------------------------------------------------------------------
            // Program.cs
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Program.cs", @"

                using System;
                using System.Collections.Generic;
                using System.Text;
                using System.Resources;
                using System.Reflection;

                namespace ConsoleApplication37
                {
	                class Program
	                {
		                static int Main(string[] args)
		                {
                            try
                            {
			                    ResourceManager rm;
                    			
			                    rm = new ResourceManager(`ConsoleApplication37.Strings1`, Assembly.GetExecutingAssembly());
			                    Console.WriteLine(rm.GetString(`Usage`));
                    			
			                    rm = new ResourceManager(`MyStrings2`, Assembly.GetExecutingAssembly());
			                    Console.WriteLine(String.Format(rm.GetString(`InvalidChildElement`), `Foo`));

			                    rm = new ResourceManager(`ConsoleApplication37.Subfolder.Strings3`, Assembly.GetExecutingAssembly());
			                    Console.WriteLine(rm.GetString(`CopyrightMessage`));

                                return 0;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                                return 1;
                            }
		                }
	                }
                }

                ");

            // ----------------------------------------------------------------------------
            // Strings1.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Strings1.restext", 
                @"Usage=Hello world!  Isn't it a beautiful day?");

            // ----------------------------------------------------------------------------
            // Strings2.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Strings2.restext", 
                @"InvalidChildElement=The element {0} is not allowed here.");

            // ----------------------------------------------------------------------------
            // Subfolder\Strings3.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Subfolder\Strings3.restext", 
                @"CopyrightMessage=Copyright (C) 2005, The MSBuild Team");

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"ConsoleApplication37.csproj");

            string stdout = ObjectModelHelpers.RunTempProjectBuiltApplication(@"bin\debug\ConsoleApplication37.exe");

            Assert.IsTrue(@"ConsoleApplication37.exe did not emit Usage string.  See Standard Out tab for details.", 
                stdout.Contains("Hello world!  Isn't it a beautiful day?"));

            Assert.IsTrue(@"ConsoleApplication37.exe did not emit InvalidChildElement string.  See Standard Out tab for details.", 
                stdout.Contains("The element Foo is not allowed here."));

            Assert.IsTrue(@"ConsoleApplication37.exe did not emit CopyrightMessage string.  See Standard Out tab for details.", 
                stdout.Contains("Copyright (C) 2005, The MSBuild Team"));
        }

        /// <summary>
        /// Tests that we correctly handle .RESTEXT files marked as EmbeddedResource in the project.
        /// </summary>
        [Test]
        public void ResTextFiles_VB()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ----------------------------------------------------------------------------
            // ConsoleApplication38.vbproj
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication38.vbproj", @"

                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{136C326F-8E8F-4164-9AFC-CA8BC754F103}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <StartupObject>ConsoleApplication38.Module1</StartupObject>
                        <RootNamespace>ConsoleApplication38</RootNamespace>
                        <AssemblyName>ConsoleApplication38</AssemblyName>
                        <MyType>Console</MyType>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <DefineDebug>true</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DocumentationFile>ConsoleApplication38.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <DefineDebug>false</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DocumentationFile>ConsoleApplication38.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Deployment` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Import Include=`Microsoft.VisualBasic` />
                        <Import Include=`System` />
                        <Import Include=`System.Collections` />
                        <Import Include=`System.Collections.Generic` />
                        <Import Include=`System.Data` />
                        <Import Include=`System.Diagnostics` />
                        <Import Include=`System.Reflection` />
                        <Import Include=`System.Resources` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Module1.vb` />
                        <EmbeddedResource Include=`Strings1.restext` />
                        <EmbeddedResource Include=`Strings2.restext`>
                            <LogicalName>MyStrings2.resources</LogicalName>
                        </EmbeddedResource>
                        <EmbeddedResource Include=`Subfolder\Strings3.restext` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                </Project>

                ");

            // ----------------------------------------------------------------------------
            // Module1.vb
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Module1.vb", @"

                Module Module1

                    Sub Main()
                        Try
                            Dim rm1 As New ResourceManager(`ConsoleApplication38.Strings1`, Assembly.GetExecutingAssembly())
                            Console.WriteLine(rm1.GetString(`Usage`))

                            Dim rm2 As New ResourceManager(`MyStrings2`, Assembly.GetExecutingAssembly())
                            Console.WriteLine(String.Format(rm2.GetString(`InvalidChildElement`), `Foo`))

                            Dim rm3 As New ResourceManager(`ConsoleApplication38.Strings3`, Assembly.GetExecutingAssembly())
                            Console.WriteLine(rm3.GetString(`CopyrightMessage`))

                        Catch ex As Exception

                            Console.WriteLine(ex.ToString())

                        End Try
                    End Sub

                End Module

                ");

            // ----------------------------------------------------------------------------
            // Strings1.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Strings1.restext", 
                @"Usage=Hello world!  Isn't it a beautiful day?");

            // ----------------------------------------------------------------------------
            // Strings2.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Strings2.restext", 
                @"InvalidChildElement=The element {0} is not allowed here.");

            // ----------------------------------------------------------------------------
            // Subfolder\Strings3.restext
            // ----------------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"Subfolder\Strings3.restext", 
                @"CopyrightMessage=Copyright (C) 2005, The MSBuild Team");

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"ConsoleApplication38.vbproj");

            string stdout = ObjectModelHelpers.RunTempProjectBuiltApplication(@"bin\debug\ConsoleApplication38.exe");

            Assert.IsTrue(@"ConsoleApplication38.exe did not emit Usage string.  See Standard Out tab for details.", 
                stdout.Contains("Hello world!  Isn't it a beautiful day?"));

            Assert.IsTrue(@"ConsoleApplication38.exe did not emit InvalidChildElement string.  See Standard Out tab for details.", 
                stdout.Contains("The element Foo is not allowed here."));

            Assert.IsTrue(@"ConsoleApplication38.exe did not emit CopyrightMessage string.  See Standard Out tab for details.", 
                stdout.Contains("Copyright (C) 2005, The MSBuild Team"));
        }
    }

    /// <summary>
    /// Regress specific bugs.
    /// </summary>
    [TestFixture]
    sealed public class Bugs
    {
        /// <summary>
        /// In this bug, calling Project.EvaluatedProperties cached properties and the next call
        /// to run a target didn't invalidate the cache. The result was that the TargetFrameworkDirectory
        /// wasn't visible to VS.
        /// </summary>
        [Test]
        public void Regress381480()
        {
            string f0 = FileUtilities.GetTemporaryFile();
            string f1 = FileUtilities.GetTemporaryFile();
            try
            {
                Project p0 = ObjectModelHelpers.CreateInMemoryProject(@"
    
                        <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                            <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                        </Project>

                    ");

                Project p1 = ObjectModelHelpers.CreateInMemoryProject(@"
    
                        <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                           <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                        </Project>

                    ");

                p0.FullPath = f0;
                p1.FullPath = f1;

                p0.Build("GetFrameworkPaths");
                Dictionary<string, string> preEvaluated = p1.EvaluatedProperties;
                p1.Build("GetFrameworkPaths");
                Dictionary<string, string> postEvaluated = p1.EvaluatedProperties;

                Assert.IsTrue("Expected a value for TargetFrameworkDirectory.", postEvaluated["TargetFrameworkDirectory"].Value.Length > 0);
                Assert.IsTrue("Expected a different property group.", preEvaluated != postEvaluated);
            }
            finally
            {
                File.Delete(f0);
                File.Delete(f1);
            }

        }
    }

    /// <summary>
    /// Tests the MainBuiltProjectOutputGroup Target which is responsible for quickly (i.e. without
    /// building) returning the name of the EXE or DLL that would be built.
    /// </summary>
    [TestFixture]
    sealed public class GetTargetPath        
    {
        /// <summary>
        /// Try a basic workings.
        /// </summary>
        [Test]
        public void Basic()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                      <PropertyGroup>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`Class1.cs` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                   </Project>
                ");            
            
            Hashtable h = new Hashtable();
            p.Build(new string[] {"GetTargetPath" }, h);
            ObjectModelHelpers.AssertSingleItemInDictionary(h, "<|proj|>bin\\Debug\\MyAssembly.exe");
        }
    }    

    /// <summary>
    /// Tests that exercise the PrepareResourceNames Target in 
    /// Microsoft.VisualBasic.targets.
    /// 
    /// This target's job is to create manifest resource names for each of
    /// the resource files.
    /// </summary>
    [TestFixture]
    sealed public class PrepareResourceNamesTarget        
    {
        /// <summary>
        /// Basic test.
        /// </summary>
        [Test]
        public void BasicVbResourceNames()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                   <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                      <PropertyGroup>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <RootNamespace>MyNamespace</RootNamespace>
                        <MSBuildTargetsVerbose>true</MSBuildTargetsVerbose>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`Class1.vb` />
                        <EmbeddedResource Include=`Resource1.txt` />
                        <EmbeddedResource Include=`Resource2.resx` />
                        <EmbeddedResource Include=`Resource2.fr.resx` />
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                    </Project>
                ");            

            p.Build(new string [] {"PrepareResourceNames"}, null);

            ProjectItem[] items = p.GetItems("EmbeddedResource").ToArray();            

            Assert.AreEqual("Resource2.resx", items[0].EvaluatedInclude);
            Assert.AreEqual("false", items[0].GetMetadataValue("WithCulture"));
            Assert.AreEqual("Resx", items[0].GetMetadataValue("Type"));
            Assert.AreEqual("MyNamespace.Resource2", items[0].GetMetadataValue("ManifestResourceName"));

            Assert.AreEqual("Resource2.fr.resx", items[1].EvaluatedInclude);
            Assert.AreEqual("true", items[1].GetMetadataValue("WithCulture"));
            Assert.AreEqual("Resx", items[1].GetMetadataValue("Type"));
            Assert.AreEqual("MyNamespace.Resource2.fr", items[1].GetMetadataValue("ManifestResourceName"));            

            Assert.AreEqual("Resource1.txt", items[2].EvaluatedInclude);
            Assert.AreEqual("false", items[2].GetMetadataValue("WithCulture"));
            Assert.AreEqual("Non-Resx", items[2].GetMetadataValue("Type"));
            Assert.AreEqual("MyNamespace.Resource1.txt", items[2].GetMetadataValue("ManifestResourceName"));
        }
    }

    /// <summary>
    /// Tests the CopyAppConfigFile target.
    /// </summary>
    [TestFixture]
    sealed public class CopyAppConfigFile
    {
        [Test]
        public void CopyAppConfigFileEvenForDllProjects()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                    <ProductVersion>8.0.50413</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <OutputType>Library</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>ClassLibrary16</RootNamespace>
                    <AssemblyName>ClassLibrary16</AssemblyName>
                </PropertyGroup>
                <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                </PropertyGroup>
                <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                </PropertyGroup>
                <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                </ItemGroup>
                <ItemGroup>
                    <Compile Include=`Class1.cs` />
                </ItemGroup>
                <ItemGroup>
                    <None Include=`App.config` />
                </ItemGroup>
                <ItemGroup>
                    <Folder Include=`Properties\` />
                </ItemGroup>
                <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                    Other similar extension points exist, see Microsoft.Common.targets.
                <Target Name=`BeforeBuild`>
                </Target>
                <Target Name=`AfterBuild`>
                </Target>
                -->
                </Project>
            ");
            
            // ---------------------
            // Class1.cs
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                using System;
                using System.Collections.Generic;
                using System.Text;
                using System.Reflection;
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;

                // General Information about an assembly is controlled through the following 
                // set of attributes. Change these attribute values to modify the information
                // associated with an assembly.
                [assembly: AssemblyTitle(`ClassLibrary16`)]
                [assembly: AssemblyDescription(``)]
                [assembly: AssemblyConfiguration(``)]
                [assembly: AssemblyCompany(`Microsoft`)]
                [assembly: AssemblyProduct(`ClassLibrary16`)]
                [assembly: AssemblyCopyright(`Copyright © Microsoft 2005`)]
                [assembly: AssemblyTrademark(``)]
                [assembly: AssemblyCulture(``)]

                // Setting ComVisible to false makes the types in this assembly not visible 
                // to COM components.  If you need to access a type in this assembly from 
                // COM, set the ComVisible attribute to true on that type.
                [assembly: ComVisible(false)]

                // The following GUID is for the ID of the typelib if this project is exposed to COM
                [assembly: Guid(`3c11545e-2e63-403b-bd07-5fb2e0f78c92`)]

                // Version information for an assembly consists of the following four values:
                //
                //      Major Version
                //      Minor Version 
                //      Build Number
                //      Revision
                //
                // You can specify all the values or you can default the Revision and Build Numbers 
                // by using the '*' as shown below:
                [assembly: AssemblyVersion(`1.0.0.0`)]
                [assembly: AssemblyFileVersion(`1.0.0.0`)]

                namespace ClassLibrary16
                {
	                public class Class1
	                {
	                }
                }
            ");

            // ---------------------
            // App.config
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("App.config", @"
                <?xml version=`1.0` encoding=`utf-8` ?>
                <configuration>
                </configuration>
            ");

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj");

            Assert.IsTrue(@"Did not find expected file bin\debug\ClassLibrary16.dll.config",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\ClassLibrary16.dll.config")));
        }

        /// <summary>
        /// Find app.config in various locations
        /// </summary>
        /// <remarks>
        ///    The search order is:
        ///
        ///    (1) Choose the value $(AppConfig) set in the main project.
        ///    (2) Choose @(None) App.Config in the same folder as the project.
        ///    (3) Choose @(Content) App.Config in the same folder as the project.
        ///    (4) Choose @(None) App.Config in any subfolder in the project.
        ///    (5) Choose @(Content) App.Config in any subfolder in the project.
        ///
        ///If an app.config is not found in one of these locations then there is no app.config for this project.
        /// </remarks>
        [Test]
        public void AppConfigLocation()
        {
            // Try each of the cases in turn, by manipulating a single project
            ProjectCollection e = new ProjectCollection();
            e.SetGlobalProperty("case", "0"); // Make project loadable
            Project p = ObjectModelHelpers.CreateInMemoryProject(e, @"

                   <Project ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                      <PropertyGroup>
                        <OutputType>Library</OutputType>
                        <AssemblyName>foo</AssemblyName>
                        <AppConfig Condition=`'$(case)'&lt;=1`>foo.exe.config</AppConfig>
                      </PropertyGroup>
                      <ItemGroup>
                        <None Include=`app.config` Condition=`'$(case)'&lt;=2`/>
                        <Content Include=`app.config` Condition=`'$(case)'&lt;=3 and '$(case)'!=2`/>
                        <None Include=`foo\app.config` Condition=`'$(case)'&lt;=4`/>
                        <Content Include=`bar\app.config` Condition=`'$(case)'&lt;=5`/>
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                    </Project>
                ", null);

            ///    (1) Choose the value $(AppConfig) set in the main project.
            p.SetGlobalProperty("case", "1");
            p.Build(new string[] { "PrepareForBuild" });
            ProjectItemm item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", "foo.exe.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));

            ///    (2) Choose @(None) App.Config in the same folder as the project.
            p.SetGlobalProperty("case", "2");
            p.Build(new string[] { "PrepareForBuild" });
            item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", "app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));

            ///    (3) Choose @(Content) App.Config in the same folder as the project.
            p.SetGlobalProperty("case", "3");
            p.Build(new string[] { "PrepareForBuild" });
            item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", "app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));
            
            ///    (4) Choose @(None) App.Config in any subfolder in the project.
            p.SetGlobalProperty("case", "4");
            p.Build(new string[] { "PrepareForBuild" });
            item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", "foo\\app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));

            ///    (5) Choose @(Content) App.Config in any subfolder in the project.
            p.SetGlobalProperty("case", "5");
            p.Build(new string[] { "PrepareForBuild" });
            item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", "bar\\app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));

            ///If an app.config is not found in one of these locations then there is no app.config for this project.
            p.SetGlobalProperty("case", "6");
            p.Build(new string[] { "PrepareForBuild" });
            ObjectModelHelpers.AssertNoItem(p, "AppConfigWithTargetPath");
        }

        /// <summary>
        /// Handle app.config's specified with paths like "..\..\app.config"
        /// In this case both app.config's do not match exactly "app.config" so we should take the /last/
        /// match listed. This arbitrary choice matches the behavior we shipped.
        /// </summary>
        [Test]
        public void AppConfigLocationRelativeDir()
        {
            ProjectCollection e = new ProjectCollection();
            Project p = ObjectModelHelpers.CreateInMemoryProject(e, @"

                   <Project ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                      <PropertyGroup>
                        <OutputType>Library</OutputType>
                        <AssemblyName>foo</AssemblyName>
                      </PropertyGroup>
                      <ItemGroup>
                        <None Include=`..\..\app.config`/>
                        <None Include=`.\app.config`/>
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                    </Project>
                ", null);

            ///    Pick the last one
            p.Build(new string[] { "PrepareForBuild" });
            ProjectItemm item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", @".\app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));
        }

        /// <summary>
        /// None should be chosen in preference to Content
        /// </summary>
        [Test]
        public void AppConfigLocationNoneWinsOverContent()
        {
            ProjectCollection e = new ProjectCollection();
            Project p = ObjectModelHelpers.CreateInMemoryProject(e, @"

                   <Project ToolsVersion=`4.0` xmlns=`msbuildnamespace`> 
                      <PropertyGroup>
                        <OutputType>Library</OutputType>
                        <AssemblyName>foo</AssemblyName>
                      </PropertyGroup>
                      <ItemGroup>
                        <None Include=`c:\foo\app.config`/>
                        <Content Include=`d:\bar\app.config`/>
                      </ItemGroup>
                      <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                    </Project>
                ", null);

            ///    Pick the last one, trying None first
            p.Build(new string[] { "PrepareForBuild" });
            ProjectItemm item = ObjectModelHelpers.AssertSingleItem(p, "AppConfigWithTargetPath", @"c:\foo\app.config");
            Assert.AreEqual("foo.dll.config", item.GetMetadataValue("TargetPath"));
        }
    }

    /// <summary>
    /// Tests some general things about our .TARGETS files, such as which properties are referenced.
    /// </summary>
    [TestFixture]
    sealed public class General
    {
        /// <summary>
        /// Tests that our .TARGETS files do not condition on $(Configuration), thereby adding
        /// configs to the VS config dropdown when they don't really exist in the project file.
        /// </summary>
        [Test]
        public void ConfigurationsReferencedInCSharpProject()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50502</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{083A5AF7-1AD5-416F-8770-BE564F54DA22}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>ConsoleApplication27</RootNamespace>
                        <AssemblyName>ConsoleApplication27</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'FooConfig|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Program.cs` />
                        <Compile Include=`Properties\AssemblyInfo.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");            

            string[] configurations = p.GetConditionedProperties("Configuration");

            Console.WriteLine("Configurations found = " + String.Join(", ", configurations));
            Assert.AreEqual("See Standard Out tab for details", 1, configurations.Length);
            Assert.AreEqual("See Standard Out tab for details", "FooConfig", configurations[0]);
        }

        /// <summary>
        /// Tests that our .TARGETS files do not condition on $(Configuration), thereby adding
        /// configs to the VS config dropdown when they don't really exist in the project file.
        /// </summary>
        [Test]
        public void ConfigurationsReferencedInVBProject()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50502</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{DB0F2DFA-0164-4071-902D-348330C940E6}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <StartupObject>ConsoleApplication28.Module1</StartupObject>
                        <RootNamespace>ConsoleApplication28</RootNamespace>
                        <AssemblyName>ConsoleApplication28</AssemblyName>
                        <MyType>Console</MyType>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'FooConfig|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <DefineDebug>true</DefineDebug>
                        <DefineTrace>true</DefineTrace>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DocumentationFile>ConsoleApplication28.xml</DocumentationFile>
                        <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Deployment` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Import Include=`Microsoft.VisualBasic` />
                        <Import Include=`System` />
                        <Import Include=`System.Collections` />
                        <Import Include=`System.Collections.Generic` />
                        <Import Include=`System.Data` />
                        <Import Include=`System.Diagnostics` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Module1.vb` />
                        <Compile Include=`My Project\AssemblyInfo.vb` />
                        <Compile Include=`My Project\Application.Designer.vb`>
                        <AutoGen>True</AutoGen>
                        <DependentUpon>Application.myapp</DependentUpon>
                        </Compile>
                        <Compile Include=`My Project\Resources.Designer.vb`>
                        <AutoGen>True</AutoGen>
                        <DesignTime>True</DesignTime>
                        <DependentUpon>Resources.resx</DependentUpon>
                        </Compile>
                        <Compile Include=`My Project\Settings.Designer.vb`>
                        <AutoGen>True</AutoGen>
                        <DependentUpon>Settings.settings</DependentUpon>
                        <DesignTimeSharedInput>True</DesignTimeSharedInput>
                        </Compile>
                    </ItemGroup>
                    <ItemGroup>
                        <EmbeddedResource Include=`My Project\Resources.resx`>
                        <Generator>VbMyResourcesResXFileCodeGenerator</Generator>
                        <LastGenOutput>Resources.Designer.vb</LastGenOutput>
                        <CustomToolNamespace>My.Resources</CustomToolNamespace>
                        </EmbeddedResource>
                    </ItemGroup>
                    <ItemGroup>
                        <None Include=`My Project\Application.myapp`>
                        <Generator>MyApplicationCodeGenerator</Generator>
                        <LastGenOutput>Application.Designer.vb</LastGenOutput>
                        </None>
                        <None Include=`My Project\Settings.settings`>
                        <Generator>SettingsSingleFileGenerator</Generator>
                        <LastGenOutput>Settings.Designer.vb</LastGenOutput>
                        </None>
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.VisualBasic.targets` />
                </Project>

                ");            

            string[] configurations = p.GetConditionedProperties("Configuration");

            Console.WriteLine("Configurations found = " + String.Join(", ", configurations));
            Assert.AreEqual("See Standard Out tab for details", 1, configurations.Length);
            Assert.AreEqual("See Standard Out tab for details", "FooConfig", configurations[0]);
        }

        /// <summary>
        /// This is the infamous path-too-long problem.  All absolute paths in question are within 
        /// the 260 character limit that the filesystem imposes.  However, when paths are accessed 
        /// using relative paths, sometimes the simple concatenation of the current directory with the
        /// relative path can exceed 260 characters.  MSBuild should solve this scenario by doing 
        /// smarter path manipulation.
        /// </summary>
        [Test]
        public void ProjectToProjectReferenceWithLongRelativePath()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            string tempProjectRoot = ObjectModelHelpers.TempProjectDir;

            // Set deepRelativePath = abc\abc\abc\ ... \abc\abc\abc\

            // minus 55 to leave room for ConsoleApp\obj\debug\ResolveAssemblyReference.cache
            // div 4 because that's how much each subdir costs.
            int MAX_PATH = 260;
            int numberOfSubDirectoriesToCreate = (MAX_PATH - tempProjectRoot.Length - 55) / 4;  
            StringBuilder deepRelativePath = new StringBuilder();
            for (int i = 0 ; i < numberOfSubDirectoriesToCreate ; i++)
            {
                deepRelativePath.Append(@"abc\");
            }

            // Set relativePathToConsoleAppDir = abc\abc\abc\ ... \abc\abc\abc\ConsoleApp\
            string relativePathToConsoleAppDir = deepRelativePath.ToString() + @"ConsoleApp\";

            // Set relativePathToClassLibDir = abc\abc\abc\ ... \abc\abc\abc\ClassLib\
            string relativePathToClassLibDir = deepRelativePath.ToString() + @"ClassLib\";

            // ====================================
            // ConsoleApp\ConsoleApp.csproj
            // ====================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(relativePathToConsoleAppDir + "ConsoleApp.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <CleanFile>CleanFile.txt</CleanFile>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <OutputType>Exe</OutputType>
                        <RootNamespace>ConsoleApp</RootNamespace>
                        <AssemblyName>ConsoleApp</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Program.cs` />
                    </ItemGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\..\..\..\..\..\..\abc\abc\abc\abc\abc\abc\ClassLib\ClassLib.csproj` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            // ====================================
            // ConsoleApp\Program.cs
            // ====================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(relativePathToConsoleAppDir + "Program.cs", @"
                namespace ConsoleApplication79
                {
	                class Program
	                {
		                static void Main(string[] args)
		                {
			                ClassLibrary1.LongPathBug foo = new ClassLibrary1.LongPathBug();
		                }
	                }
                }
                ");

            // ====================================
            // ClassLib\ClassLib.csproj
            // ====================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(relativePathToClassLibDir + "ClassLib.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <CleanFile>CleanFile.txt</CleanFile>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <OutputType>Library</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>ClassLib</RootNamespace>
                        <AssemblyName>ClassLib</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                    <!-- The old OM, which is what this solution is being built under, doesn't understand
                         BeforeTargets, so this test was failing, because _AssignManagedMetadata was set 
                         up as a BeforeTarget for Build.  Copied here so that build will return the correct
                         information again. -->
                    <Target Name=`BeforeBuild`>
                        <ItemGroup>
                            <BuiltTargetPath Include=`$(TargetPath)`>
                                <ManagedAssembly>$(ManagedAssembly)</ManagedAssembly>
                            </BuiltTargetPath>
                        </ItemGroup>
                    </Target>
                </Project>
                ");

            // ====================================
            // ClassLib\Class1.cs
            // ====================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(relativePathToClassLibDir + "Class1.cs", @"
                namespace ClassLibrary1
                {
	                public class LongPathBug
	                {
	                }
                }
                ");


            // Build the ConsoleApp project.
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess(relativePathToConsoleAppDir + "ConsoleApp.csproj");

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(relativePathToConsoleAppDir + @"bin\Debug\ConsoleApp.exe");
        }

        /// <summary>
        /// There is a C# project that has a P2P ref to a J# project.  The C# project supports Debug/Release|AnyCPU.
        /// The J# project supports Debug/Release|x86.  There is a solution configuration defined call Debug/Release|Mixed Platforms
        /// which contains the appropriate project configurations.
        /// </summary>
        [Test]
        //[Ignore("Need J# to be in the v3.5 folder")]
        public void SolutionConfigurationWithDifferentProjectConfigurations()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ==================================================================
            // SOLUTION1.SLN
            // ==================================================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Solution1.sln", 
                @"Microsoft Visual Studio Solution File, Format Version 9.00
                 Visual Studio 2005
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `CSharpClassLib`, `CSharpClassLib\CSharpClassLib.csproj`, `{9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}`
                EndProject
                Project(`{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}`) = `JSharpClassLib`, `JSharpClassLib\JSharpClassLib.vjsproj`, `{DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}`
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Debug|Any CPU = Debug|Any CPU
		                Debug|Mixed Platforms = Debug|Mixed Platforms
		                Debug|x86 = Debug|x86
		                Release|Any CPU = Release|Any CPU
		                Release|Mixed Platforms = Release|Mixed Platforms
		                Release|x86 = Release|x86
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Debug|Mixed Platforms.ActiveCfg = Debug|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Debug|Mixed Platforms.Build.0 = Debug|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Debug|x86.ActiveCfg = Debug|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Release|Any CPU.Build.0 = Release|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Release|Mixed Platforms.ActiveCfg = Release|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Release|Mixed Platforms.Build.0 = Release|Any CPU
		                {9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}.Release|x86.ActiveCfg = Release|Any CPU
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Debug|Any CPU.ActiveCfg = Debug|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Debug|Mixed Platforms.ActiveCfg = Debug|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Debug|Mixed Platforms.Build.0 = Debug|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Debug|x86.ActiveCfg = Debug|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Debug|x86.Build.0 = Debug|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Release|Any CPU.ActiveCfg = Release|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Release|Mixed Platforms.ActiveCfg = Release|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Release|Mixed Platforms.Build.0 = Release|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Release|x86.ActiveCfg = Release|x86
		                {DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}.Release|x86.Build.0 = Release|x86
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
                EndGlobal
                ");

            // ==================================================================
            // CSHARPCLASSLIB.CSPROJ
            // ==================================================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"CSharpClassLib\CSharpClassLib.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                    <ProductVersion>8.0.50627</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{9FB32A10-FA44-4DD3-ABA8-5215CF599BD6}</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>CSharpClassLib</RootNamespace>
                    <AssemblyName>CSharpClassLib</AssemblyName>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=`Class1.cs` />
                  </ItemGroup>
                  <ItemGroup>
                    <ProjectReference Include=`..\JSharpClassLib\JSharpClassLib.vjsproj`>
                      <Project>{DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}</Project>
                      <Name>JSharpClassLib</Name>
                    </ProjectReference>
                  </ItemGroup>
                  <ItemGroup>
                    <Folder Include=`Properties\` />
                  </ItemGroup>
                  <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=`BeforeBuild`>
                  </Target>
                  <Target Name=`AfterBuild`>
                  </Target>
                  -->
                </Project>
                ");

            // ==================================================================
            // CLASS1.CS
            // ==================================================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"CSharpClassLib\Class1.cs", @"
                using System;
                using System.Collections.Generic;
                using System.Text;

                namespace CSharpClassLib
                {
	                public class Class1
	                {
		                JSharpClassLib.Class1 myjsharpclass = new JSharpClassLib.Class1();
	                }
                }
                ");

            // ==================================================================
            // JSHARPCLASSLIB.VJSPROJ
            // ==================================================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"JSharpClassLib\JSharpClassLib.vjsproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>x86</Platform>
                    <ProductVersion>8.0.50627</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{DFE7D1F5-B0E8-4EB8-BC1B-0274C2747078}</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <RootNamespace>JSharpClassLib</RootNamespace>
                    <AssemblyName>JSharpClassLib</AssemblyName>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|x86' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|x86' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=`Class1.jsl` />
                  </ItemGroup>
                  <ItemGroup>
                    <Folder Include=`Properties\` />
                  </ItemGroup>
                  <Import Project=`$(MSBuildBinPath)\Microsoft.VisualJSharp.targets` />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=`BeforeBuild`>
                  </Target>
                  <Target Name=`AfterBuild`>
                  </Target>
                  -->
                </Project>
                ");

            // ==================================================================
            // CLASS1.JSL
            // ==================================================================
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"JSharpClassLib\Class1.jsl", @"
                package JSharpClassLib;


                /**
                 * Summary description for Class1
                 */
                public class Class1
                {
	                public Class1()
	                {
		                //
		                // TO DO: Add constructor logic here
		                //
	                }
                }
                ");

            // Build the .SLN
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("Solution1.sln");

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"CSharpClassLib\bin\debug\CSharpClassLib.dll");
        }

        /// <summary>
        /// Tests that the .pdb file is not copied to the output directory when the
        /// SkipCopyingSymbolsToOutputDirectory property is set.
        /// </summary>
        [Test]
        public void SkipCopyingPdbFile()
        {
            // create a temp project
            Helper.CreateTempCSharpProjectWithClassLibrary();

            // build it and expect the .pdb to be in the output directory
            ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, null);
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // set the SkipCopyingSymbolsToOutputDirectory property
            Dictionary<string, string> additionalProperties = new Dictionary<string, string> ();
            additionalProperties["SkipCopyingSymbolsToOutputDirectory"] = "true";

            // build the project again and expect the .pdb to have been removed from the output directory
            ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, additionalProperties);
            ObjectModelHelpers.AssertFileDoesNotExistInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // set the SkipCopyingSymbolsToOutputDirectory property explicitly to "false"
            additionalProperties["SkipCopyingSymbolsToOutputDirectory"]= "false";

            // build the project again and expect the .pdb to be back in the output directory
            ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, null);
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");
        }

        /// <summary>
        /// Tests that the .pdb file is not produced if /p:debugtype=none is set externally
        /// </summary>
        [Test]
        public void SkipProducingPdbCsharp()
        {
            // create a temp project
            Helper.CreateTempCSharpProjectWithClassLibrary();

            // build it and expect the .pdb to be in the output directory: verify /debug+ /debug:full is default for debug config
            MockLogger l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, null);
            l.AssertLogContains("/debug+");
            l.AssertLogContains("/debug:full");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // verify /debug:pdbonly is default for release config
            Dictionary<string, string> additionalProperties = new Dictionary<string, string> ();
            additionalProperties.SetProperty("Configuration", "release");
            l.ClearLog();
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, additionalProperties, true);
            l.AssertLogDoesntContain("/debug+");
            l.AssertLogContains("/debug:pdbonly");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\release\ConsoleApplication.pdb");

            // set the DebugSymbols=false property
            additionalProperties = new Dictionary<string, string> ();
            additionalProperties.SetProperty("DebugType", "none");

            // build the project again and expect the .pdb to have been removed from the output directory
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, additionalProperties, true);
            l.AssertLogDoesntContain("/debug+");
            l.AssertLogContains("/debug-");
            l.AssertLogDoesntContain("/debug:full");
            ObjectModelHelpers.AssertFileDoesNotExistInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // debug config again; set the DebugType property explicitly to "full"
            additionalProperties = new Dictionary<string, string>();
            additionalProperties["DebugType"] ="full";

            // build the project again and expect the .pdb to be back in the output directory
            l.ClearLog();
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, additionalProperties, true);
            l.AssertLogContains("/debug+");
            l.AssertLogContains("/debug:full");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // try release configuration with DebugSymbols set to true, as well
            additionalProperties = new Dictionary<string, string>();
            additionalProperties["Configuration"] = "release";
            additionalProperties["DebugSymbols"] = "true";
            l.ClearLog();
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.csproj", null, additionalProperties, true);
            l.AssertLogContains("/debug+");
            l.AssertLogContains("/debug:pdbonly");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\release\ConsoleApplication.pdb");        
        }

        /// <summary>
        /// Tests that the .pdb file is not produced if /p:debugsymbols=false is set externally
        /// </summary>
        [Test]
        public void SkipProducingPdbVB()
        {
            // create a temp project
            Helper.CreateTempVBProject();

            // build it and expect the .pdb to be in the output directory
            MockLogger l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.vbproj", null, null, true);
            l.AssertLogContains("/debug+");
            l.AssertLogContains("/debug:full");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            //// set the DebugType=none property
            Dictionary<string, string> additionalProperties = new Dictionary<string, string> ();
            additionalProperties.SetProperty("DebugType", "none");

            // build the project again and expect the .pdb to have been removed from the output directory
            l.ClearLog();
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.vbproj", null, additionalProperties, true);
            l.AssertLogDoesntContain("/debug+");
            l.AssertLogContains("/debug-");
            l.AssertLogDoesntContain("/debug:full");
            ObjectModelHelpers.AssertFileDoesNotExistInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");

            // set the DebugType property explicitly to "full"
            additionalProperties.SetProperty("DebugType", "full");

            // build the project again and expect the .pdb to be back in the output directory
            l.ClearLog();
            l = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"ConsoleApplication\ConsoleApplication.vbproj", null, null, true);
            l.AssertLogContains("/debug+");
            l.AssertLogContains("/debug:full");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"ConsoleApplication\bin\Debug\ConsoleApplication.pdb");
        }
    }

    /// <summary>
    /// Helper methods for unit-tests in this file.
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// Creates a temporary project on disk for doing unit-tests on.
        /// </summary>
        internal static void CreateTempCSharpProjectWithClassLibrary()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication\ConsoleApplication.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                    <ProductVersion>8.0.50727</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{34D5E50A-464A-4098-9DB6-679D5310E7EB}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>ConsoleApplication</RootNamespace>
                    <AssemblyName>ConsoleApplication</AssemblyName>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=`Program.cs` />
                  </ItemGroup>
                  <ItemGroup>
                    <ProjectReference Include=`..\ClassLibrary\ClassLibrary.csproj`>
                      <Project>{50E656DA-C81B-43D5-B2ED-8B5DCB2398EB}</Project>
                      <Name>ClassLibrary1</Name>
                    </ProjectReference>
                  </ItemGroup>
                  <ItemGroup>
                    <Folder Include=`Properties\` />
                  </ItemGroup>
                  <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication\Program.cs", @"
                using System;
                using System.Collections.Generic;
                using System.Text;

                namespace ConsoleApplication1
                {
                    class Program
                    {
                        static void Main(string[] args)
                        {
                        }
                    }
                }
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ClassLibrary\ClassLibrary.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                    <ProductVersion>8.0.50727</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{50E656DA-C81B-43D5-B2ED-8B5DCB2398EB}</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>ClassLibrary</RootNamespace>
                    <AssemblyName>ClassLibrary</AssemblyName>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DefineConstants>TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=`Class.cs` />
                  </ItemGroup>
                  <ItemGroup>
                    <Folder Include=`Properties\` />
                  </ItemGroup>
                  <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ClassLibrary\Class.cs", @"
                using System;
                using System.Collections.Generic;
                using System.Text;

                namespace ClassLibrary1
                {
                    public class Class1
                    {
                    }
                }
                ");
        }

        /// <summary>
        /// Creates a temporary project on disk for doing unit-tests on.
        /// </summary>
        internal static void CreateTempVBProject()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication\ConsoleApplication.vbproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`4.0` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProductVersion>8.0.60512</ProductVersion>
                    <SchemaVersion>2.0</SchemaVersion>
                    <ProjectGuid>{32D53832-D685-4FF7-B093-8ADCE0CA9F20}</ProjectGuid>
                    <OutputType>Exe</OutputType>
                    <StartupObject>VBconsoleapp.Module1</StartupObject>
                    <RootNamespace>VBconsoleapp</RootNamespace>
                    <AssemblyName>ConsoleApplication</AssemblyName>
                    <FileAlignment>512</FileAlignment>
                    <MyType>Console</MyType>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <DefineDebug>true</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DocumentationFile>VBconsoleapp.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                  </PropertyGroup>
                  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
                    <DebugType>pdbonly</DebugType>
                    <DefineDebug>false</DefineDebug>
                    <DefineTrace>true</DefineTrace>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                    <DocumentationFile>VBconsoleapp.xml</DocumentationFile>
                    <NoWarn>42016,41999,42017,42018,42019,42032,42036,42020,42021,42022</NoWarn>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System"" />
                    <Reference Include=""System.Data"" />
                    <Reference Include=""System.Deployment"" />
                    <Reference Include=""System.Drawing"" />
                    <Reference Include=""System.Windows.Forms"" />
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
                    <Import Include=""System.Drawing"" />
                    <Import Include=""System.Diagnostics"" />
                    <Import Include=""System.Windows.Forms"" />
                    <Import Include=""System.Linq"" />
                    <Import Include=""System.Xml.Linq"" />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=""Module1.vb"" />
                  </ItemGroup>
                  <Import Project=""$(MSBuildBinPath)\Microsoft.VisualBasic.targets"" />
                  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
                       Other similar extension points exist, see Microsoft.Common.targets.
                  <Target Name=""BeforeBuild"">
                  </Target>
                  <Target Name=""AfterBuild"">
                  </Target>
                  -->
            </Project>");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"ConsoleApplication\Module1.vb", @"
                Module Module1

                    Sub Main()

                    End Sub

                End Module
                ");
        }
#endif
    }
}
