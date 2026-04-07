// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Shouldly;
using Xunit;
using System.Linq;

using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests
{
    public class WriteAllTextDetailedTest
    {
        [Fact]
        public async Task File_WriteAllText_ChecksDiagnosticCount()
        {
            var diags = await GetDiagnosticsAsync(@"
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        File.WriteAllText(""file.txt"", ""contents"");
                        return true;
                    }
                }
            ");

            var pathDiags = diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).ToArray();

            pathDiags.Length.ShouldBe(1, $"Expected exactly 1 diagnostic but got {pathDiags.Length}: {string.Join(", ", pathDiags.Select(d => d.GetMessage()))}");
        }
    }
}
