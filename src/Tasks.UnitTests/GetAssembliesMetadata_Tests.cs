// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class GetAssembliesMetadata_Tests
    {
        private static string TestAssembliesPaths { get; } = Path.Combine(AppContext.BaseDirectory, "TestResources", "Assemblies");

        [Fact]
        public void CheckPresenceOfCustomCOMAssemblyAttributes()
        {
            string assemblyPath = Path.Combine(TestAssembliesPaths, "Custom_COM.dll");
            GetAssembliesMetadata t = new() { AssemblyPaths = new[] { assemblyPath } };

            bool isSuccess = t.Execute();

            isSuccess.ShouldBeTrue();
            t.AssembliesMetadata[0].ItemSpec.ShouldBe(assemblyPath);
            t.AssembliesMetadata[0].GetMetadata("AssemblyName").ShouldBe("Custom_COM");
            t.AssembliesMetadata[0].GetMetadata("IsImportedFromTypeLib").ShouldBe("False");
            t.AssembliesMetadata[0].GetMetadata("RevisionNumber").ShouldBe("4");
            t.AssembliesMetadata[0].GetMetadata("IsAssembly").ShouldBe("True");
            t.AssembliesMetadata[0].GetMetadata("RuntimeVersion").ShouldBe("v4.0.30319");
            t.AssembliesMetadata[0].GetMetadata("MajorVersion").ShouldBe("1");
            t.AssembliesMetadata[0].GetMetadata("MinorVersion").ShouldBe("2");
            t.AssembliesMetadata[0].GetMetadata("PeKind").ShouldBe("3");
            t.AssembliesMetadata[0].GetMetadata("Guid").ShouldBe("a48efb66-2596-4c6a-87ab-c8a765e54429");
            t.AssembliesMetadata[0].GetMetadata("BuildNumber").ShouldBe("3");
            t.AssembliesMetadata[0].GetMetadata("Description").ShouldBe("description for com");
            t.AssembliesMetadata[0].GetMetadata("Culture").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("TargetFrameworkMoniker").ShouldBe(".NETFramework,Version=v4.7.2");
            t.AssembliesMetadata[0].GetMetadata("DefaultAlias").ShouldBe("Custom_COM");
            t.AssembliesMetadata[0].GetMetadata("PublicKey").ShouldBeEmpty();
        }

        [Fact]
        public void CheckPresenceOfCOMAssemblyAttributes()
        {
            string assemblyPath = Path.Combine("%SystemRoot%", "Microsoft.NET", "Framework", "v4.0.30319", "mscorlib.dll");
            GetAssembliesMetadata t = new() { AssemblyPaths = new[] { assemblyPath } };

            bool isSuccess = t.Execute();

            isSuccess.ShouldBeTrue();
            t.AssembliesMetadata[0].ItemSpec.ShouldBe(assemblyPath);
            t.AssembliesMetadata[0].GetMetadata("AssemblyName").ShouldBe("mscorlib");
            t.AssembliesMetadata[0].GetMetadata("IsImportedFromTypeLib").ShouldBe("False");
            t.AssembliesMetadata[0].GetMetadata("RevisionNumber").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("IsAssembly").ShouldBe("True");
            t.AssembliesMetadata[0].GetMetadata("RuntimeVersion").ShouldBe("v4.0.30319");
            t.AssembliesMetadata[0].GetMetadata("MajorVersion").ShouldBe("4");
            t.AssembliesMetadata[0].GetMetadata("MinorVersion").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("PeKind").ShouldBe("1");
            t.AssembliesMetadata[0].GetMetadata("Guid").ShouldBe("BED7F4EA-1A96-11d2-8F08-00A0C9A6186D");
            t.AssembliesMetadata[0].GetMetadata("BuildNumber").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("Description").ShouldBe("mscorlib.dll");
            t.AssembliesMetadata[0].GetMetadata("Culture").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("TargetFrameworkMoniker").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("DefaultAlias").ShouldBe("mscorlib");
            t.AssembliesMetadata[0].GetMetadata("PublicKey").ShouldBe("00000000000000000400000000000000");
        }
    }
}
#endif
