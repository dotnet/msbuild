// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;

using Shouldly;

using Xunit;

namespace Microsoft.Build.Engine.UnitTests.Evaluation
{
    public class IntrinsicFunctionOverload_Tests
    {
        private Version ChangeWaveForOverloading = ChangeWaves.Wave17_8;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MSBuildAddInteger(bool enableIntrinsicFunctionOverloads)
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Add($([System.Int64]::MaxValue), 1))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = enableIntrinsicFunctionOverloads ? unchecked(long.MaxValue + 1).ToString() : (long.MaxValue + 1.0).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            if (!enableIntrinsicFunctionOverloads)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaveForOverloading.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
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

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MSBuildSubtractInteger(bool enableIntrinsicFunctionOverloads)
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Subtract($([System.Int64]::MaxValue), 9223372036854775806))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = enableIntrinsicFunctionOverloads ? 1.ToString() : 0.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            if (!enableIntrinsicFunctionOverloads)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaveForOverloading.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
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

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MSBuildMultiplyInteger(bool enableIntrinsicFunctionOverloads)
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Multiply($([System.Int64]::MaxValue), 2))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = enableIntrinsicFunctionOverloads ? unchecked(long.MaxValue * 2).ToString() : (long.MaxValue * 2.0).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            if (!enableIntrinsicFunctionOverloads)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaveForOverloading.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
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

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MSBuildDivideInteger(bool enableIntrinsicFunctionOverloads)
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Divide(10, 3))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = enableIntrinsicFunctionOverloads ? (10 / 3).ToString() : (10.0 / 3.0).ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            if (!enableIntrinsicFunctionOverloads)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaveForOverloading.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
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

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MSBuildModuloInteger(bool enableIntrinsicFunctionOverloads)
        {
            const string projectContent = @"
                    <Project>
                        <PropertyGroup>
                            <Actual>$([MSBuild]::Modulo(10, 3))</Actual>
                        </PropertyGroup>
                    </Project>";

            string expected = 1.ToString();

            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            if (!enableIntrinsicFunctionOverloads)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaveForOverloading.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
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

            var project = new Project(XmlReader.Create(new StringReader(projectContent.Cleanup())));
            ProjectProperty? actualProperty = project.GetProperty("Actual");
            actualProperty.EvaluatedValue.ShouldBe(expected);
        }
    }
}
