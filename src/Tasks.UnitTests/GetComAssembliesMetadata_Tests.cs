// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#if FEATURE_APPDOMAIN

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
    public class GetComAssembliesMetadata_Tests
    {
        private static string TestAssembliesPaths { get; } = Path.Combine(AppContext.BaseDirectory, "TestResources", "Assemblies");

        [Fact]
        public void CheckPresenceOfCustomCOMAssemblyAttributes()
        {
            string assemblyPath = Path.Combine(TestAssembliesPaths, "Custom_COM.dll");
            GetComAssembliesMetadata t = new() { AssembyPaths = new[] { assemblyPath } };

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
            t.AssembliesMetadata[0].GetMetadata("PeKind").ShouldBe("1");
            t.AssembliesMetadata[0].GetMetadata("Guid").ShouldBe("a48efb66-2596-4c6a-87ab-c8a765e54429");
            t.AssembliesMetadata[0].GetMetadata("BuildNumber").ShouldBe("3");
            t.AssembliesMetadata[0].GetMetadata("Description").ShouldBe("description for com");
            t.AssembliesMetadata[0].GetMetadata("Culture").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("TargetFrameworkMoniker").ShouldBe(".NETFramework,Version=v4.7.2");
            t.AssembliesMetadata[0].GetMetadata("DefaultAlias").ShouldBe("Custom_COM");
            t.AssembliesMetadata[0].GetMetadata("PublicKey").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("PublicKeyLength").ShouldBe("0");
        }

        [Fact]
        public void CheckPresenceOfCOMAssemblyAttributes()
        {
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string programFilesRefAssemblyLocation = Path.Combine(programFilesX86, "Reference Assemblies\\Microsoft\\Framework");
            string assemblyPath = Path.Combine(programFilesRefAssemblyLocation, ".NETFramework", "v4.7.2", "mscorlib.dll");
            GetComAssembliesMetadata t = new() { AssembyPaths = new[] { assemblyPath } };

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
            t.AssembliesMetadata[0].GetMetadata("PublicKeyLength").ShouldBe("16");
        }
    }
}
#endif
