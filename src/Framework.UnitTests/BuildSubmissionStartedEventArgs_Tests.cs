// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class BuildSubmissionStartedEventArgs_Tests
    {
        [Fact]
        public void SerializationDeserializationTest()
        {
            var globalVariables = new Dictionary<string, string?>
            {
                {"Variable1", "Value1" },
                {"Variable2", "" },
                {"Variable3", null },
            };

            var entryPointProjects = new List<string>()
            {
                "project1",
                "project2",
                "",
            };
            var targetNames = new List<string>()
            {
                "target1",
                "target2",
                "",
            };
            var flag = Execution.BuildRequestDataFlags.FailOnUnresolvedSdk;
            var submissionId = 1234;

            BuildSubmissionStartedEventArgs args = new(
                globalVariables,
                entryPointProjects,
                targetNames,
                flag,
                submissionId);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            BuildSubmissionStartedEventArgs argDeserialized = new();
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

            argDeserialized.CreateFromStream(br, packetVersion);
            argDeserialized.GlobalProperties.ShouldBe(globalVariables);
            argDeserialized.EntryProjectsFullPath.ShouldBe(entryPointProjects);
            argDeserialized.TargetNames.ShouldBe(targetNames);
            argDeserialized.Flags.ShouldBe(flag);
            argDeserialized.SubmissionId.ShouldBe(submissionId);
        }
    }
}
