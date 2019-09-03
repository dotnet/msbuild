// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{

    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Microsoft.Build.Evaluation;
    using Xunit;

    public class TestCollectionGroup : IDisposable
    {
        public static string SampleProjectFile = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='2.0' InitialTargets='it' DefaultTargets='dt'>
                        <PropertyGroup Condition=""'$(Configuration)'=='Foo'"">
                            <p>v1</p>
                            <gpt1>Foo$(gp1)</gpt1>
                        </PropertyGroup>
                        <PropertyGroup Condition=""'$(Configuration)'!='Foo'"">
                            <p>v2</p>
                            <gpt1>NotFoo$(gp1)</gpt1>
                        </PropertyGroup>
                        <PropertyGroup>
                            <p2>X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Condition=""'$(Configuration)'=='Foo'"" Include='i0'/>
                            <i Include='i1'/>
                            <i Include='$(p)X;i3'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <task/>
                        </Target>
                    </Project>
                ");

        public static string BigProjectFile = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='2.0' InitialTargets='it' DefaultTargets='dt'>
                        <Import Project='pi1.proj' />
                        <Import Project='pi2.proj' Condition=""'$(Configuration)'=='Foo'""/>
                        <Import Project='pi3.proj' Condition='false' Sdk=""FakeSdk"" Version=""1.0"" MinimumVersion=""1.0""/>

                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter1 ParameterType='System.String' Output='true' Required='false'/>
                              <MyParameter2 ParameterType='System.String' Output='true' Required='false'/>
                           </ParameterGroup>
                       </UsingTask>

                        <UsingTask TaskName='LooserTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <Task Evaluate='false'>Contents</Task>
                           <ParameterGroup>
                              <YourParameter1 ParameterType='System.String' Output='true' Required='false'/>
                              <YourParameter2 ParameterType='System.String' Output='true' Required='false'/>
                           </ParameterGroup>
                       </UsingTask>

                        <ImportGroup>
                            <Import Project='a.proj' />
                            <Import Project='b.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='false'>
                            <Import Project='c.proj' />
                        </ImportGroup>


                        <PropertyGroup Condition=""'$(Configuration)'=='Foo'"">
                            <p>v1</p>
                        </PropertyGroup>
                        <PropertyGroup Condition=""'$(Configuration)'!='Foo'"">
                            <p>v2</p>
                        </PropertyGroup>
                        <PropertyGroup>
                            <p2>X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Condition=""'$(Configuration)'=='Foo'"" Include='i0'/>
                            <i Include='i1'/>
                            <i Include='$(p)X;i3'/>
                            <i2 Include='item2' KeepDuplicates=""false"" KeepMetadata=""CopyToOutputDirectory;TargetPath"" RemoveMetadata=""xx""/>
                            <i2 Remove='item2'/>
                            <i2 Update='item2'/>
                        </ItemGroup>

                        <ItemGroup>
                            <src Condition=""'$(Configuration)'=='Foo'"" Include='foo.cs'/>
                            <src Include='foo2.cs'/>
                            <i4 Include='i' Exclude='j' m2='v2' />
                        </ItemGroup>

                        <ItemGroup Label=""Group1"">
                            <Compile Include=""Constants.cs"">
                                <ExcludeFromStyleCop>true</ExcludeFromStyleCop>
                            </Compile>
                            <Compile Include=""EncodingStringWriter.cs"">
                                <Link>EncodingStringWriter.cs</Link>
                            </Compile>
                            <Compile Include=""EncodingUtilities.cs"">
                                 <Link>EncodingUtilities.cs</Link>
                            </Compile>
                        </ItemGroup>

                        <ItemDefinitionGroup >
                            <i2 m1='v1'>
                                <m2 Condition='true'>v2</m2>
                                <m1>v3</m1>
                            </i2>
                        </ItemDefinitionGroup>

                        <ItemDefinitionGroup>
                            <i3 m1='v1'>
                                <m1>v3</m1>
                            </i3>
                            <i4 />
                        </ItemDefinitionGroup>

                        <Choose>
                            <When Condition=""'$(Configuration)'=='Foo'"">
                              <PropertyGroup>
                                <p>vFoo</p>
                              </PropertyGroup> 
                            </When>
                            <When Condition='false'>
                              <PropertyGroup>
                                <p>vFalse</p>
                              </PropertyGroup> 
                            </When>      
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>vTrue</p>
                              </PropertyGroup> 
                            </When>      
                            <Otherwise>
                              <PropertyGroup>
                                <p>vOtherwise</p>
                              </PropertyGroup> 
                            </Otherwise>
                        </Choose>

                        <Target Name='t'>
                            <task/>
                        </Target>

                        <Target
                            Name=""Frankenstein""
                            Label=""Target1""
                            Returns=""@(_ProjectReferencesFromRAR);@(_ResolvedNativeProjectReferencePaths)""
                            Inputs=""@(_SourceItemsToCopyToOutputDirectory)""
                            BeforeTargets=""Compile""
                            AfterTargets=""Link""
                            Outputs=""@(_SourceItemsToCopyToOutputDirectory->'$(OutDir)%(TargetPath)')""
                            KeepDuplicateOutputs="" '$(MSBuildDisableGetCopyToOutputDirectoryItemsOptimization)' == '' ""
                            DependsOnTargets=""ResolveProjectReferences;ResolveAssemblyReferences"">

                            <ItemGroup>
                                <_ProjectReferencesFromRAR Include=""@(ReferencePath->WithMetadataValue('ReferenceSourceTarget', 'ProjectReference'))"">
                                <OriginalItemSpec>%(ReferencePath.ProjectReferenceOriginalItemSpec)</OriginalItemSpec>
                                </_ProjectReferencesFromRAR>

                                <Compile Include=""Constants.cs"">
                                    <ExcludeFromStyleCop>true</ExcludeFromStyleCop>
                                </Compile>
                                <Compile Include=""EncodingStringWriter.cs"">
                                    <Link>EncodingStringWriter.cs</Link>
                                </Compile>

                            </ItemGroup>

                            <FindAppConfigFile PrimaryList=""@(None)"" SecondaryList=""@(Content)"" TargetPath=""$(TargetFileName).config"" Condition=""'$(AppConfig)'==''"">
                                <Output TaskParameter=""AppConfigFile"" ItemName=""AppConfigWithTargetPath""/>
                                <Output TaskParameter=""AppConfigFile"" PropertyName=""AppConfig""/>
                            </FindAppConfigFile>

                            <MakeDir Directories=""$(OutDir);$(IntermediateOutputPath);@(DocFileItem->'%(RelativeDir)');@(CreateDirectory)"" ContinueOnError=""True""/>

                            <OnError ExecuteTargets='1'/>
                            <OnError ExecuteTargets='2'/>
                        </Target>

                        <Sdk Name=""sdkName"" Version=""version"" MinimumVersion=""minVersion"" />

                       <ProjectExtensions>
                         <a>x</a>
                         <b>y</b>
                       </ProjectExtensions>
                    </Project>
                ");
        public int RemoteCount { get; }

        internal ProjectCollectionLinker.ConnectedProjectCollections Group { get; }
        internal ProjectCollectionLinker Local { get; }

        internal ProjectCollectionLinker[] Remote { get; } = new ProjectCollectionLinker[2];
        internal TransientIO Disk { get; }
        protected TransientIO ImmutableDisk { get; }
        public IReadOnlyList<string> StdProjectFiles { get; }

        private IReadOnlyDictionary<ProjectCollectionLinker, HashSet<Project>> ImmutableProjects { get; set; }

        protected void TakeSnapshot()
        {
            Assert.Null(this.ImmutableProjects);
            var result = new Dictionary<ProjectCollectionLinker, HashSet<Project>>();
            this.Local.Importing = false;
            result.Add(this.Local, new HashSet<Project>(this.Local.Collection.LoadedProjects));
            foreach (var r in this.Remote)
            {
                r.Importing = false;
                result.Add(r, new HashSet<Project>(r.Collection.LoadedProjects));
            }

            this.ImmutableProjects = result;
        }

        public TestCollectionGroup(int remoteCount, int stdFilesCount)
        {
            this.RemoteCount = 2;

            this.Group = ProjectCollectionLinker.CreateGroup();

            this.Local = this.Group.AddNew();
            this.Remote = new ProjectCollectionLinker[this.RemoteCount];
            for (int i = 0; i < this.RemoteCount; i++)
            {
                this.Remote[i] = this.Group.AddNew();
            }

            this.ImmutableDisk = new TransientIO();
            this.Disk = this.ImmutableDisk.GetSubFolder("Mutable");

            List<string> stdFiles = new List<string>();
            for (int i=0; i< stdFilesCount; i++)
            {
                stdFiles.Add(this.ImmutableDisk.WriteProjectFile($"Proj{i}.proj", TestCollectionGroup.SampleProjectFile));
            }

            this.StdProjectFiles = stdFiles;
        }

        private void Clear(ProjectCollectionLinker linker)
        {
            linker.Importing = false;
            HashSet<Project> toKeep = null;
            this.ImmutableProjects?.TryGetValue(linker, out toKeep);
            if (toKeep == null)
            {
                linker.Collection.UnloadAllProjects();
            }
            else
            {
                var toUnload = new List<Project>();
                foreach (var p in linker.Collection.LoadedProjects)
                {
                    if (!toKeep.Contains(p))
                        toUnload.Add(p);
                }

                foreach (var p in toUnload)
                {
                    linker.Collection.UnloadProject(p);
                    linker.Collection.UnloadProject(p.Xml);
                }
            }
        }
        public void Clear()
        {
            this.Clear(this.Local);
            foreach (var remote in this.Remote)
            {
                this.Clear(remote);
            }

            this.Group.ClearAllRemotes();
            this.Disk.Clear();
        }

        public void Dispose()
        {
            this.Clear();
            this.ImmutableDisk.Dispose();
        }
    }
}
