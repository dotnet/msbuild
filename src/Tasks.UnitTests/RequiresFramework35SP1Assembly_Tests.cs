// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class RequiresFramework35SP1Assembly_Tests
    {
        private readonly ITestOutputHelper _output;

        public RequiresFramework35SP1Assembly_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private RequiresFramework35SP1Assembly CreateTask() =>
            new()
            {
                BuildEngine = new MockEngine(_output),
            };

        [Fact]
        public void Defaults_UncheckedSigningTriggers()
        {
            // The task's default SigningManifests=false is itself an "unchecked signing" trigger,
            // so the all-defaults case reports RequiresMinimumFramework35SP1=true.
            RequiresFramework35SP1Assembly task = CreateTask();

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void SigningEnabled_NoOtherSignals_DoesNotTrigger()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeFalse();
        }

        [Fact]
        public void ErrorReportUrl_Triggers()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            task.ErrorReportUrl = "https://example.com/errors";

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void CreateDesktopShortcut_OnNet35_Triggers()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            task.TargetFrameworkVersion = "v3.5";
            task.CreateDesktopShortcut = true;

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void SuiteName_Triggers()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            task.SuiteName = "MyAppSuite";

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Theory]
        [InlineData("ReferencedAssemblies")]
        [InlineData("Assemblies")]
        [InlineData("Files")]
        [InlineData("DeploymentManifestEntryPoint")]
        [InlineData("EntryPoint")]
        public void IncludeHashFalse_OnAnyItemInput_Triggers(string inputName)
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            ITaskItem item = new TaskItem("some.dll", new Dictionary<string, string?> { { "IncludeHash", "false" } });
            AssignItemInput(task, inputName, item);

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void CreateDesktopShortcut_OnNet20_DoesNotTrigger()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            task.TargetFrameworkVersion = "v2.0";
            task.CreateDesktopShortcut = true;

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeFalse();
        }

        [Fact]
        public void CreateDesktopShortcut_BareVersionString_Works()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            task.TargetFrameworkVersion = "3.5";
            task.CreateDesktopShortcut = true;

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void Sp1AssemblyIdentity_TriggersWithoutIncludeHash()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            AssignItemInput(task, "Files", new TaskItem("System.Data.Entity"));

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        [Fact]
        public void Net35ClientSentinelIdentity_Triggers()
        {
            RequiresFramework35SP1Assembly task = CreateTask();
            task.SigningManifests = true;
            AssignItemInput(task, "Assemblies", new TaskItem("Sentinel.v3.5Client"));

            task.Execute().ShouldBeTrue();
            task.RequiresMinimumFramework35SP1.ShouldBeTrue();
        }

        private static void AssignItemInput(RequiresFramework35SP1Assembly task, string inputName, ITaskItem item)
        {
            switch (inputName)
            {
                case "ReferencedAssemblies":
                    task.ReferencedAssemblies = [item];
                    break;
                case "Assemblies":
                    task.Assemblies = [item];
                    break;
                case "Files":
                    task.Files = [item];
                    break;
                case "DeploymentManifestEntryPoint":
                    task.DeploymentManifestEntryPoint = item;
                    break;
                case "EntryPoint":
                    task.EntryPoint = item;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(inputName));
            }
        }
    }
}
