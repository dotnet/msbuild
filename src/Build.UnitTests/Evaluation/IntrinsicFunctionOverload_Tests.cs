// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

using Shouldly;

using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.Engine.UnitTests.Evaluation
{
    [UseInvariantCulture]
    public class IntrinsicFunctionOverload_Tests
    {
        [Fact]
        public void MSBuildAddInteger()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Add($([System.Int64]::MaxValue), 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = unchecked(long.MaxValue + 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildAddIntegerGreaterThanMax()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Add(9223372036854775808, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MaxValue + 1D) + 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildAddIntegerLessThanMin()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Add(-9223372036854775809, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MinValue - 1D) + 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildAddReal()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Add(1.0, 2.0))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 3.0.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildSubtractInteger()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Subtract($([System.Int64]::MaxValue), 9223372036854775806))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 1.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void FileExists_WhenFileExists_ReturnsTrue()
        {          
            using TestEnvironment env = TestEnvironment.Create();

            string testFilePath = Path.Combine(env.DefaultTestDirectory.Path, "TestFile.txt");
            File.WriteAllText(testFilePath, "Test content");

            string projectContent = $@"
                <Project>
                    <PropertyGroup>
                        <TestFilePath>{testFilePath.Replace(@"\", @"\\")}</TestFilePath>
                        <FileExists>$([MSBuild]::FileExists($(TestFilePath)))</FileExists>
                    </PropertyGroup>
                </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("FileExists");
            actualProperty.EvaluatedValue.ShouldBe("True");
        }

        [Fact]
        public void FileExists_WhenFileDoesNotExist_ReturnsFalse()
        {
            const string projectContent = @"
            <Project>
                <PropertyGroup>
                    <TestFilePath>NonExistentFile.txt</TestFilePath>
                    <FileExists>$([MSBuild]::FileExists($(TestFilePath)))</FileExists>
                </PropertyGroup>
            </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("FileExists");
            actualProperty.EvaluatedValue.ShouldBe("False");
        }

        [Fact]
        public void SystemIODirectoryExists_WhenDirectoryExists_ReturnsTrue()
        {
            using TestEnvironment env = TestEnvironment.Create();
            string testDirPath = Path.Combine(env.DefaultTestDirectory.Path, "TestDir");

            Directory.CreateDirectory(testDirPath);

            string projectContent = $@"
                <Project>
                    <PropertyGroup>
                        <TestDirPath>{testDirPath.Replace(@"\", @"\\")}</TestDirPath>
                        <DirExists>$([System.IO.Directory]::Exists($(TestDirPath)))</DirExists>
                    </PropertyGroup>
                </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("DirExists");
            actualProperty.EvaluatedValue.ShouldBe("True");
        }

        [Fact]
        public void SystemIODirectoryExists_WhenDirectoryDoesNotExist_ReturnsFalse()
        {
            const string projectContent = @"
            <Project>
                <PropertyGroup>
                    <TestDirPath>TestDir</TestDirPath>
                    <DirExists>$([System.IO.Directory]::Exists($(TestDirPath)))</DirExists>
                </PropertyGroup>
            </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("DirExists");
            actualProperty.EvaluatedValue.ShouldBe("False");
        }

        [Fact]
        public void DirectoryExists_WhenDirectoryExists_ReturnsTrue()
        {
            using TestEnvironment env = TestEnvironment.Create();
            string testDirPath = Path.Combine(env.DefaultTestDirectory.Path, "TestDir");

            Directory.CreateDirectory(testDirPath);

            string projectContent = $@"
            <Project>
                <PropertyGroup>
                    <TestDirPath>{testDirPath.Replace(@"\", @"\\")}</TestDirPath>
                    <DirExists>$([MSBuild]::DirectoryExists($(TestDirPath)))</DirExists>
                </PropertyGroup>
            </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("DirExists");
            actualProperty.EvaluatedValue.ShouldBe("True");
        }

        [Fact]
        public void DirectoryExists_WhenDirectoryDoesNotExists_ReturnsFalse()
        {
            const string projectContent = @"
            <Project>
                <PropertyGroup>
                    <TestDirPath>TestDir</TestDirPath>
                    <DirExists>$([MSBuild]::DirectoryExists($(TestDirPath)))</DirExists>
                </PropertyGroup>
            </Project>";

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;

            ProjectProperty actualProperty = project.GetProperty("DirExists");
            actualProperty.EvaluatedValue.ShouldBe("False");
        }

        [Fact]
        public void MSBuildSubtractIntegerGreaterThanMax()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Subtract(9223372036854775808, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MaxValue + 1D) - 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildSubtractIntegerLessThanMin()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Subtract(-9223372036854775809, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MinValue - 1D) - 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildSubtractReal()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Subtract(2.0, 1.0))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 1.0.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildMultiplyInteger()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Multiply($([System.Int64]::MaxValue), 2))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = unchecked(long.MaxValue * 2).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildMultiplyIntegerGreaterThanMax()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Multiply(9223372036854775808, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MaxValue + 1D) * 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildMultiplyIntegerLessThanMin()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Multiply(-9223372036854775809, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MinValue - 1D) * 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildMultiplyReal()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Multiply(2.0, 1.0))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 2.0.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildDivideInteger()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Divide(10, 3))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = (10 / 3).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildDivideIntegerGreaterThanMax()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Divide(9223372036854775808, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MaxValue + 1D) / 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildDivideIntegerLessThanMin()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Divide(-9223372036854775809, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MinValue - 1D) / 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildDivideReal()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Divide(1, 0.5))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 2.0.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildModuloInteger()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Modulo(10, 3))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 1.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildModuloIntegerGreaterThanMax()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Modulo(9223372036854775808, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MaxValue + 1D) % 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildModuloIntegerLessThanMin()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Modulo(-9223372036854775809, 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = ((long.MinValue - 1D) % 1).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Fact]
        public void MSBuildModuloReal()
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Modulo(11.0, 2.5))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 1.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();

            using ProjectFromString projectFromString = new(projectContent.Cleanup());
            Project project = projectFromString.Project;
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }
    }
}
