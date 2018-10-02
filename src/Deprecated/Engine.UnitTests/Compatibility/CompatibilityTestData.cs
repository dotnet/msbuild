// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Class wrapper for static data items used in unit tests
    /// </summary>
    internal static class TestData
    {
        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleTools35 = @"
                    <Project ToolsVersion='3.5' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentUsingTaskName = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                       <UsingTask TaskName='TaskName' AssemblyName='AssemblyName' Condition='true'/>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentUsingTaskFile = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <PropertyGroup>
                            <value>true</value>
                        </PropertyGroup>  
                       <UsingTask TaskName='TaskName' AssemblyFile='AssemblyName.dll' Condition='$(value)==true'/>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentA = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <PropertyGroup>
                            <n>xmlValue</n>
                        </PropertyGroup>     
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentImportA = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='importA.proj' Condition='true'/>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentImportB = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='importB.proj' Condition='true'/>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentImport1 = @"
                    <Project ToolsVersion='4.0' InitialTargets='importTarget1a' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='ImportTarget1a'>
                            <Message Text='Executed ImportTarget 1a'/>
                        </Target>
                        <Target Name='ImportTarget1b'>
                            <Message Text='Executed ImportTarget 1b'/>
                        </Target>
                        <Target Name='ChangeImportedProperty'>
                            <Message Text='ImportedProperty is $(q)'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentImport2 = @"
                    <Project ToolsVersion='4.0' InitialTargets='importTarget2a' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='ImportTarget2a'>
                            <Message Text='Executed ImportTarget 2a'/>
                        </Target>
                        <Target Name='ImportTarget2b'>
                            <Message Text='Executed ImportTarget 2b'/>
                        </Target>
                            <PropertyGroup>
                                <ImportedProperty>xmlvalue</ImportedProperty>
                            </PropertyGroup>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleTools35InitialTargets = @"
                    <Project ToolsVersion='4.0' InitialTargets='InitialTarget' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='FirstTarget'>
                            <Message Text='Executed First Target '/>
                        </Target>
                        <Target Name='LocalTarget'>
                            <Message Text='Executed Local Target '/>
                        </Target>
                        <Target Name='InitialTarget'>
                            <Message Text='Executed Initial Target'/>
                        </Target>
                        <Target Name='TestTargetDefault'>
                            <Message Text='Executed Target Default'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleUTF16 = @"<?xml version='1.0' encoding='UTF-16'?>
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleInvalidEncoding = @"<?xml version='1.0' encoding='crazy'?>
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleCustomToolsVersion = @"
                    <Project ToolsVersion='10.10' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleInvalidXml = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentSimpleInvalidMSBuildXml = @"
                    <project ToolsVersion='4.0' INVALID='value' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='TestTarget'>
                            <Message Text='Executed TestTarget'/>
                        </Target>
                    </project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string Content3SimpleTargetsDefaultSpecified = @"
                    <Project ToolsVersion='4.0'  DefaultTargets='TestTargetDefault' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='Target1'>
                            <Message Text='Executed Target 1'/>
                        </Target>
                        <Target Name='Target2'>
                            <Message Text='Executed Target 2'/>
                        </Target>
                        <Target Name='TestTargetDefault'>
                            <Message Text='Executed Target Default'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string Content3SimpleTargetsNoDefaultSpecified = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='Target1' DependsOnTargets='Target2'>
                            <Message Text='Executed Target 1'/>
                        </Target>
                        <Target Name='Target2'>
                            <Message Text='Executed Target 2'/>
                        </Target>
                        <Target Name='Target3'>
                            <Message Text='Executed Target 3'/>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentCreatePropertyTarget = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <PropertyGroup>
                            <n>xmlValue</n>
                        </PropertyGroup>                        
                        <Target Name='CreatePropertyTarget'>
                            <CreateProperty Value='v'>
                                  <Output TaskParameter='Value' PropertyName='p'/>
                            </CreateProperty>
                        </Target>
                        <Target Name='Target1' DependsOnTargets='CreatePropertyTarget'>
                            <Message Text='Executed Target 1'/>
                        </Target>
                        <Target Name='Target2'>
                            <Message Text='Executed Target 2'/>
                        </Target>
                        <Target Name='printn'>
                            <Message Text='value is $(n)'/>
                        </Target>
                       
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentCreateItemTarget = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='CreateItemTarget'>
                            <CreateItem Include='i'>
                                  <Output TaskParameter='Include' ItemName='BuildItem' />
                            </CreateItem>
                        </Target>
                        <Target Name='Target1' DependsOnTargets='CreatePropertyTarget'>
                            <Message Text='Executed Target 1'/>
                        </Target>
                        <Target Name='Target2'>
                            <Message Text='Executed Target 2'/>
                        </Target>                           
                    </Project>
                ";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentInvalidTargetsWithOutput = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='Target1' Outputs='$(p);@(i);$(q)'>
                            <CreateProperty Value='v'>
                                <Output TaskParameter='Value' PropertyName='p' />
                            </CreateProperty>
                            <CreateItem Value='a'>
                                <Output TaskParameter='Value' ItemName='i' />
                            </CreateItem>
                        </Target>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentValidTargetsWithOutput = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <PropertyGroup>
                                <x>y</x>
                            </PropertyGroup>
                        <Target Name='Target1' Outputs='$(p);@(i);$(q)'>
                            <CreateProperty Value='v'>
                                <Output TaskParameter='Value' PropertyName='p' />
                            </CreateProperty>
                            <CreateItem Include='a'>
                                <Output TaskParameter='Include' ItemName='i' />
                            </CreateItem>
                            <ItemGroup>
                                <i Include='b'/>
                            </ItemGroup>
                            <PropertyGroup>
                                <q>u</q>
                            </PropertyGroup>
                        </Target>
                          
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ItemGroup = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <i Include='a' Condition='1==0'/>
                                <i Include='b' Condition='true'/>
                                <i Include='c' Condition='true'/>
                            </ItemGroup>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ItemGroup2 = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup Condition='true'>
                                <j Include='2a' Condition='1==0'/>
                                <j Include='2b' Condition='true'/>
                                <j Include='2c' Condition='true'/>
                            </ItemGroup>
                            <ItemGroup>
                                <i2 Include='2a' Condition='1==0'/>
                                <i2 Include='2b' Condition='true'/>
                                <i2 Include='2c' Condition='true'/>
                            </ItemGroup>  
                            <ItemGroup>
                                <k Include='2a' Condition='1==0'/>
                                <k Include='2b' Condition='true'/>
                                <k Include='2c' Condition='true'/>
                            </ItemGroup>
                           
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ItemGroup3 = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup Condition='true'>
                                <i Include='2a' Condition='1==0'/>
                                <j Include='2b' Condition='true'/>
                                <j Include='2c' Condition='true'/>
                            </ItemGroup>
                            <PropertyGroup>
                                <q>u</q>
                            </PropertyGroup>
                            <ItemGroup >
                                <i Include='2a' Condition='1==0'/>
                                <i Include='2b' Condition='true'/>
                                <i Include='2c' Condition='true'/>
                            </ItemGroup>  
                            <ItemGroup>
                                <i Include='2a' Condition='1==0'/>
                                <i Include='2b' Condition='true'/>
                                <i Include='2c' Condition='true'/>
                            </ItemGroup>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string PropertyGroup = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <PropertyGroup Condition='true'>
                                <n1 Condition='true'>v1</n1>
                                <n2>v2</n2>
                                <n3>v3</n3>
                            </PropertyGroup>
                    </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentMissingImports = @"
                          <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                            <Import Project='$(MSBuildBinPath)\Target.DoesNotExist.targets' />
                            <Import Project='$(MSBuildBinPath)\Microsoft.Common.targets' />
                          </Project>";

        /// <summary>
        /// Test Data Item
        /// </summary>
        internal const string ContentExtensions = @"
                          <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                             <ProjectExtensions>
                                    <id1>v1</id1>
                                    <id2>v2</id2>
                                    <id3 xmlns='http://schemas.microsoft.com/developer/msbuild/2003' />
                             </ProjectExtensions>
                          </Project>";
    }
}
