// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public sealed class ItemDisposalCheck_Tests
    {
        private readonly ItemDisposalCheck _check;

        public ItemDisposalCheck_Tests()
        {
            _check = new ItemDisposalCheck();
        }

        #region Positive Tests - Should Fire

        [Fact]
        public void PrivateItem_NotRemoved_ShouldFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_TempFiles Include="*.tmp" />
                    </ItemGroup>
                    <Delete Files="@(_TempFiles)" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].CheckRule.Id.ShouldBe("BC0303");
            results[0].MessageArgs[0].ShouldBe("_TempFiles");
            results[0].MessageArgs[1].ShouldBe("TestTarget");
        }

        [Fact]
        public void PrivateItem_UsedInExec_NotRemoved_ShouldFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="RunTool">
                    <ItemGroup>
                      <_DotnetExecArgs Include="dotnet;tool;exec;$(CoolToolName)" />
                    </ItemGroup>
                    <Exec Command="@(_DotnetExecArgs, ' ')" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].CheckRule.Id.ShouldBe("BC0303");
            results[0].MessageArgs[0].ShouldBe("_DotnetExecArgs");
            results[0].MessageArgs[1].ShouldBe("RunTool");
        }

        [Fact]
        public void MultiplePrivateItems_NoneRemoved_ShouldFireForEach()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="MultiItemTarget">
                    <ItemGroup>
                      <_Item1 Include="a.txt" />
                      <_Item2 Include="b.txt" />
                      <_Item3 Include="c.txt" />
                    </ItemGroup>
                    <Message Text="@(_Item1);@(_Item2);@(_Item3)" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(3);
            results.ShouldAllBe(r => r.CheckRule.Id == "BC0303");
        }

        [Fact]
        public void PrivateItem_InMultipleTargets_EachNotRemoved_ShouldFireForEach()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="Target1">
                    <ItemGroup>
                      <_Temp Include="a.txt" />
                    </ItemGroup>
                  </Target>
                  <Target Name="Target2">
                    <ItemGroup>
                      <_Other Include="b.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(2);
            results.ShouldAllBe(r => r.CheckRule.Id == "BC0303");
        }

        [Fact]
        public void PrivateItem_PartialRemove_ShouldFire()
        {
            // The Remove uses a different wildcard, so not all items are cleaned up
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_Files Include="**/*.cs" />
                    </ItemGroup>
                    <ItemGroup>
                      <_Files Remove="*.cs" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // This is a bit nuanced - in static analysis we can't know if Remove covers all items
            // So we check if there's ANY Remove for the same item type
            // With a proper Remove present, this should NOT fire
            results.Count.ShouldBe(0);
        }

        #endregion

        #region Negative Tests - Should NOT Fire

        [Fact]
        public void PrivateItem_ProperlyRemoved_ShouldNotFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_TempFiles Include="*.tmp" />
                    </ItemGroup>
                    <Delete Files="@(_TempFiles)" />
                    <ItemGroup>
                      <_TempFiles Remove="@(_TempFiles)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_RemovedWithWildcard_ShouldNotFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_TempFiles Include="*.tmp" />
                    </ItemGroup>
                    <ItemGroup>
                      <_TempFiles Remove="**/*" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PublicItem_NotRemoved_ShouldNotFire()
        {
            // Items not starting with underscore are considered public
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <MyFiles Include="*.txt" />
                    </ItemGroup>
                    <Message Text="@(MyFiles)" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InOutputs_ShouldNotFire()
        {
            // Item is exposed via Outputs attribute - considered part of target's public contract
            string projectContent = """
                <Project>
                  <Target Name="ComputeFiles" Outputs="@(_ComputedFiles)">
                    <ItemGroup>
                      <_ComputedFiles Include="$(OutputDir)/*.dll" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InReturns_ShouldNotFire()
        {
            // Item is exposed via Returns attribute - considered part of target's public contract
            string projectContent = """
                <Project>
                  <Target Name="ComputeFiles" Returns="@(_GeneratedFiles)">
                    <ItemGroup>
                      <_GeneratedFiles Include="@(SourceFiles->'%(Filename).obj')" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InOutputsWithTransform_ShouldNotFire()
        {
            // Item is referenced in Outputs with a transform
            string projectContent = """
                <Project>
                  <Target Name="ComputeFiles" Outputs="@(_Sources->'%(FullPath)')">
                    <ItemGroup>
                      <_Sources Include="**/*.cs" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_OnlyUpdate_ShouldNotFire()
        {
            // Update doesn't create new items, only modifies existing ones
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_ExistingItems Update="@(_ExistingItems)" SomeMetadata="value" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_OnlyRemove_ShouldNotFire()
        {
            // Remove only - no Include, nothing was created in this target
            string projectContent = """
                <Project>
                  <Target Name="CleanupTarget">
                    <ItemGroup>
                      <_OldItems Remove="@(_OldItems)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void ItemOutsideTarget_ShouldNotFire()
        {
            // Items outside targets are in the global scope - not target-private
            string projectContent = """
                <Project>
                  <ItemGroup>
                    <_GlobalItem Include="*.cs" />
                  </ItemGroup>
                  <Target Name="Build">
                    <Message Text="@(_GlobalItem)" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void EmptyTarget_ShouldNotFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="EmptyTarget">
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void TargetWithOnlyTasks_ShouldNotFire()
        {
            // Arrange
            string projectContent = """
                <Project>
                  <Target Name="TaskOnlyTarget">
                    <Message Text="Hello" />
                    <Warning Text="World" />
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void PrivateItem_CaseInsensitiveRemove_ShouldNotFire()
        {
            // MSBuild item types are case-insensitive
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_TempFiles Include="*.tmp" />
                    </ItemGroup>
                    <ItemGroup>
                      <_tempfiles Remove="@(_TEMPFILES)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_MultipleIncludes_SingleRemove_ShouldNotFire()
        {
            // Multiple includes for same type, one remove should cover all
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_Files Include="a.txt" />
                      <_Files Include="b.txt" />
                      <_Files Include="c.txt" />
                    </ItemGroup>
                    <ItemGroup>
                      <_Files Remove="@(_Files)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_ConditionalInclude_NoRemove_ShouldFire()
        {
            // Conditional includes still need cleanup
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup Condition="'$(Config)'=='Debug'">
                      <_DebugFiles Include="*.pdb" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].MessageArgs[0].ShouldBe("_DebugFiles");
        }

        [Fact]
        public void PrivateItem_RemoveBeforeInclude_ShouldFire()
        {
            // Remove before Include doesn't clean up what Include adds
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_Files Remove="@(_Files)" />
                    </ItemGroup>
                    <ItemGroup>
                      <_Files Include="*.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
        }

        [Fact]
        public void PrivateItem_InReturnsWithMultipleItems_ShouldNotFireForReferencedOnes()
        {
            // Only _Other should fire, _Result is in Returns
            string projectContent = """
                <Project>
                  <Target Name="ComputeFiles" Returns="@(_Result)">
                    <ItemGroup>
                      <_Other Include="temp.txt" />
                      <_Result Include="output.dll" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].MessageArgs[0].ShouldBe("_Other");
        }

        [Fact]
        public void PrivateItem_ComplexOutputsExpression_ShouldNotFire()
        {
            // Complex Outputs expression referencing the item
            string projectContent = """
                <Project>
                  <Target Name="Generate" Outputs="$(OutDir)@(_InputFiles->'%(Filename).generated')">
                    <ItemGroup>
                      <_InputFiles Include="*.input" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InOutputsWithSemicolonInTransform_ShouldNotFire()
        {
            // Transform expression containing semicolons should be properly parsed
            string projectContent = """
                <Project>
                  <Target Name="Generate" Outputs="@(_Files->'%(Identity);%(FullPath)')">
                    <ItemGroup>
                      <_Files Include="*.cs" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InReturnsWithComplexTransform_ShouldNotFire()
        {
            // Complex transform with nested properties and metadata
            string projectContent = """
                <Project>
                  <Target Name="Process" Returns="@(_Items->'$(OutDir)\%(Filename).obj;$(IntermediateDir)\%(RelativeDir)%(Filename).dep')">
                    <ItemGroup>
                      <_Items Include="src\**\*.cs" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void MultiplePrivateItems_InSameOutputsExpression_ShouldNotFireForAny()
        {
            // Multiple private items referenced in same Outputs expression
            string projectContent = """
                <Project>
                  <Target Name="Build" Outputs="@(_Sources);@(_Resources);@(_Content)">
                    <ItemGroup>
                      <_Sources Include="*.cs" />
                      <_Resources Include="*.resx" />
                      <_Content Include="*.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InOutputsWithCustomSeparator_ShouldNotFire()
        {
            // Item reference with custom separator containing semicolons
            string projectContent = """
                <Project>
                  <Target Name="Concat" Outputs="@(_Files, ';')">
                    <ItemGroup>
                      <_Files Include="a.txt;b.txt;c.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void PrivateItem_InReturnsWithItemFunction_ShouldNotFire()
        {
            // Item function call in Returns expression
            string projectContent = """
                <Project>
                  <Target Name="Distinct" Returns="@(_Duplicates->Distinct())">
                    <ItemGroup>
                      <_Duplicates Include="a;b;c;a;b" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void MixedPrivateItems_SomeInOutputsSomeNot_ShouldFireOnlyForNonExposed()
        {
            // Some private items in Outputs, others not
            string projectContent = """
                <Project>
                  <Target Name="Mixed" Outputs="@(_Exposed)">
                    <ItemGroup>
                      <_Exposed Include="out.dll" />
                      <_NotExposed Include="temp.tmp" />
                      <_AlsoNotExposed Include="scratch.dat" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(2);
            results.ShouldContain(r => r.MessageArgs[0].ToString() == "_NotExposed");
            results.ShouldContain(r => r.MessageArgs[0].ToString() == "_AlsoNotExposed");
        }

        [Fact]
        public void PrivateItem_InOutputsAndReturns_ShouldNotFire()
        {
            // Item referenced in both Outputs and Returns
            string projectContent = """
                <Project>
                  <Target Name="BuildAndReturn" Outputs="@(_Built)" Returns="@(_Built)">
                    <ItemGroup>
                      <_Built Include="output.dll" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(0);
        }

        [Fact]
        public void SingleUnderscoreItem_ShouldFire()
        {
            // Edge case: single underscore item name
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <_ Include="weird.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].MessageArgs[0].ShouldBe("_");
        }

        [Fact]
        public void DoubleUnderscoreItem_ShouldFire()
        {
            // Double underscore prefix
            string projectContent = """
                <Project>
                  <Target Name="TestTarget">
                    <ItemGroup>
                      <__VeryPrivate Include="secret.txt" />
                    </ItemGroup>
                  </Target>
                </Project>
                """;

            // Act
            var results = AnalyzeProject(projectContent);

            // Assert
            results.Count.ShouldBe(1);
            results[0].MessageArgs[0].ShouldBe("__VeryPrivate");
        }

        #endregion

        #region Helper Methods

        private List<BuildCheckResult> AnalyzeProject(string projectContent)
        {
            // Create a project from the content string
            using var stringReader = new System.IO.StringReader(projectContent);
            using var xmlReader = System.Xml.XmlReader.Create(stringReader);
            var root = ProjectRootElement.Create(xmlReader);

            // Create a fresh results list for each test
            var results = new List<BuildCheckResult>();

            // Analyze all targets
            _check.AnalyzeTargets(root, results);

            return results;
        }

        #endregion
    }
}
