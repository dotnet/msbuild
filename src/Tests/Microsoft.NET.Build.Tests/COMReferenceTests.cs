// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class COMReferenceTests : SdkTest
    {
        public COMReferenceTests(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyTheory(Skip ="Too much dependency on build machine state.")]
        [InlineData(true)]
        [InlineData(false)]
        public void COMReferenceBuildsAndRuns(bool embedInteropTypes)
        {
            var targetFramework = "netcoreapp3.0";


            var testProject = new TestProject
            {
                Name = "UseMediaPlayer",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                IsExe = true,
                SourceFiles =
                    {
                        ["Program.cs"] = @"
                            using MediaPlayer;
                            class Program
                            {
                                static void Main(string[] args)
                                {
                                    var mediaPlayer = (IMediaPlayer2)new MediaPlayerClass();
                                }
                            }
                        ",
                }
            };

            if (embedInteropTypes)
            {
                testProject.SourceFiles.Add("MediaPlayerClass.cs", @"
                    using System.Runtime.InteropServices;
                    namespace MediaPlayer
                    {
                        [ComImport]
                        [Guid(""22D6F312-B0F6-11D0-94AB-0080C74C7E95"")]
                        class MediaPlayerClass { }
                    }
                ");
            }

            var reference = new XElement("ItemGroup",
                new XElement("COMReference",
                    new XAttribute("Include", "MediaPlayer.dll"),
                    new XElement("Guid", "22d6f304-b0f6-11d0-94ab-0080c74c7e95"),
                    new XElement("VersionMajor", "1"),
                    new XElement("VersionMinor", "0"),
                    new XElement("WrapperTool", "tlbimp"),
                    new XElement("Lcid", "0"),
                    new XElement("Isolated", "false"),
                    new XElement("EmbedInteropTypes", embedInteropTypes)));


            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: embedInteropTypes.ToString())
                .WithProjectChanges(doc => doc.Root.Add(reference));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();
            
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var runCommand = new RunExeCommand(Log, outputDirectory.File("UseMediaPlayer.exe").FullName);
            runCommand.Execute().Should().Pass();
        }
    }
}
