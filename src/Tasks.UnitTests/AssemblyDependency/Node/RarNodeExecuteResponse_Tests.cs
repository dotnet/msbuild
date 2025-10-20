// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public sealed class RarNodeExecuteResponse_Tests
    {
        [Fact]
        public void TaskOutputsArePropagated()
        {
            ResolveAssemblyReference nodeRar = new()
            {
                ResolvedFiles = [
                    new TaskItem(
                        @"C:\ProgramFiles\dotnet\net9.0\System.dll",
                        new Dictionary<string, string>()
                        {
                            { "AssemblyName", "System" },
                            { "Private", "false" },
                            { "ExternallyResolved", "True" },
                        }),
                    new TaskItem(
                        @"C:\ProgramFiles\dotnet\net9.0\System.IO.dll",
                        new Dictionary<string, string>()
                        {
                            { "AssemblyName", "System.IO" },
                            { "Private", "false" },
                            { "ExternallyResolved", "True" },
                        }),
                ],
                DependsOnNETStandard = "true",
            };

            RarNodeExecuteResponse response = new(nodeRar, success: true);

            ResolveAssemblyReference clientRar = new();
            response.SetTaskOutputs(clientRar);

            Assert.True(response.Success);
            Assert.Equal(nodeRar.ResolvedFiles.Length, clientRar.ResolvedFiles.Length);
            for (int i = 0; i < nodeRar.ResolvedFiles.Length; i++)
            {
                Assert.Equal(nodeRar.ResolvedFiles[i].ItemSpec, clientRar.ResolvedFiles[i].ItemSpec);
            }

            Assert.Equal(nodeRar.DependsOnNETStandard, clientRar.DependsOnNETStandard);
        }

        [Fact]
        public void CopyLocalFilesAreReconstructed()
        {
            ITaskItem[] copyLocalFiles = [
                new TaskItem(
                    @"C:\ProgramFiles\dotnet\net9.0\System.IO.dll",
                    new Dictionary<string, string>()
                    {
                        { "AssemblyName", "System.IO" },
                        { "Private", "false" },
                        { "ExternallyResolved", "True" },
                        { "CopyLocal", "True" },
                    }),
                new TaskItem(
                    @"C:\src\A.dll",
                    new Dictionary<string, string>()
                    {
                        { "AssemblyName", "A" },
                        { "CopyLocal", "True" },
                    }),
                new TaskItem(
                    @"C:\src\B.dll",
                    new Dictionary<string, string>()
                    {
                        { "AssemblyName", "B" },
                        { "CopyLocal", "True" },
                    }),
                new TaskItem(
                    @"C:\src\A.pdb",
                    new Dictionary<string, string>()
                    {
                        { "AssemblyName", "A" },
                        { "CopyLocal", "True" },
                    }),
                ];
            ResolveAssemblyReference nodeRar = new()
            {
                ResolvedFiles = [
                    copyLocalFiles[0],
                    new TaskItem(
                        @"C:\ProgramFiles\dotnet\net9.0\System.dll",
                        new Dictionary<string, string>()
                        {
                            { "AssemblyName", "System" },
                            { "Private", "false" },
                            { "ExternallyResolved", "True" },
                            { "CopyLocal", "False" },
                        }),
                    copyLocalFiles[1],
                ],
                ResolvedDependencyFiles = [copyLocalFiles[2]],
                RelatedFiles = [copyLocalFiles[3]],
                CopyLocalFiles = copyLocalFiles,
            };

            RarNodeExecuteResponse response = new(nodeRar, success: true);

            ResolveAssemblyReference clientRar = new();
            response.SetTaskOutputs(clientRar);

            Assert.Equal(nodeRar.CopyLocalFiles.Length, clientRar.CopyLocalFiles.Length);
            for (int i = 0; i < nodeRar.ResolvedFiles.Length; i++)
            {
                Assert.Equal(nodeRar.CopyLocalFiles[i].ItemSpec, clientRar.CopyLocalFiles[i].ItemSpec);
            }

            Assert.Equal(nodeRar.ResolvedFiles.Length, clientRar.ResolvedFiles.Length);
            for (int i = 0; i < nodeRar.ResolvedFiles.Length; i++)
            {
                Assert.Equal(nodeRar.ResolvedFiles[i].ItemSpec, clientRar.ResolvedFiles[i].ItemSpec);
            }

            Assert.Equal(nodeRar.ResolvedDependencyFiles.Length, clientRar.ResolvedDependencyFiles.Length);
            for (int i = 0; i < nodeRar.ResolvedDependencyFiles.Length; i++)
            {
                Assert.Equal(nodeRar.ResolvedDependencyFiles[i].ItemSpec, clientRar.ResolvedDependencyFiles[i].ItemSpec);
            }

            Assert.Equal(nodeRar.RelatedFiles.Length, clientRar.RelatedFiles.Length);
            for (int i = 0; i < nodeRar.RelatedFiles.Length; i++)
            {
                Assert.Equal(nodeRar.RelatedFiles[i].ItemSpec, clientRar.RelatedFiles[i].ItemSpec);
            }
        }
    }
}
