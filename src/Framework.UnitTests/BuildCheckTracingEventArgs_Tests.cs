// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class BuildCheckTracingEventArgs_Tests
    {
        [Fact]
        public void TracingDataMergesRepeatedRuleIds()
        {
            BuildCheckRuleTelemetryData first = new("id1", "check1", false, DiagnosticSeverity.Warning,
                [DiagnosticSeverity.Warning], ["project1", "shared"],
                2, 3, 5, false, TimeSpan.FromSeconds(7));
            BuildCheckRuleTelemetryData second = new("id1", "check1", false, DiagnosticSeverity.Warning,
                [DiagnosticSeverity.Error], ["project2", "shared"],
                11, 13, 17, true, TimeSpan.FromSeconds(19));
            BuildCheckRuleTelemetryData other = new("id2", "check2", true, DiagnosticSeverity.Error);
            Dictionary<string, TimeSpan> infrastructureStats = new() { ["acquisition"] = TimeSpan.FromSeconds(23) };

            first.RuleId.ShouldBe(second.RuleId);
            first.RuleId.ShouldNotBe(other.RuleId);

            BuildCheckTracingData data = new([first, other, second], infrastructureStats);

            data.TelemetryData.Count.ShouldBe(2);
            BuildCheckRuleTelemetryData expected = new("id1", "check1", false, DiagnosticSeverity.Warning,
                [DiagnosticSeverity.Warning, DiagnosticSeverity.Error], ["project1", "shared", "project2"],
                13, 16, 22, true, TimeSpan.FromSeconds(26));
            data.TelemetryData["id1"].ShouldBeEquivalentTo(expected);
            data.TelemetryData["id2"].ShouldBeEquivalentTo(other);
            data.InfrastructureTracingData.ShouldBeEquivalentTo(infrastructureStats);
        }

        [Fact]
        public void SerializationDeserializationTest()
        {
            string key1 = "AA";
            TimeSpan span1 = TimeSpan.FromSeconds(5);
            string key2 = "b";
            TimeSpan span2 = TimeSpan.FromSeconds(15);
            string key3 = "cCc";
            TimeSpan span3 = TimeSpan.FromSeconds(50);

            Dictionary<string, TimeSpan> stats = new() { { key1, span1 }, { key2, span2 }, { key3, span3 } };

            BuildCheckRuleTelemetryData ruleData1 = new("id1", "name1", true, DiagnosticSeverity.Suggestion,
                new HashSet<DiagnosticSeverity>() { DiagnosticSeverity.Default, DiagnosticSeverity.Suggestion },
                new HashSet<string>() { "aa", "b" }, 5, 2, 8, true, TimeSpan.FromSeconds(123));

            BuildCheckRuleTelemetryData ruleData2 = new("id2", "name2", false, DiagnosticSeverity.Error,
                new HashSet<DiagnosticSeverity>(),
                new HashSet<string>(), 0, 0, 500, false, TimeSpan.FromSeconds(1234));

            BuildCheckTracingData data = new(new[] { ruleData1, ruleData2 }, stats);
            BuildCheckTracingEventArgs arg = new(data);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            arg.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            BuildCheckTracingEventArgs argDeserialized = new();
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;
            argDeserialized.CreateFromStream(br, packetVersion);

            argDeserialized.TracingData.InfrastructureTracingData.ShouldBeEquivalentTo(arg.TracingData.InfrastructureTracingData);
            argDeserialized.TracingData.TelemetryData.Keys.ShouldBeEquivalentTo(arg.TracingData.TelemetryData.Keys);

            argDeserialized.TracingData.TelemetryData["id1"].ShouldBeEquivalentTo(arg.TracingData.TelemetryData["id1"]);
            argDeserialized.TracingData.TelemetryData["id2"].ShouldBeEquivalentTo(arg.TracingData.TelemetryData["id2"]);
        }
    }
}
