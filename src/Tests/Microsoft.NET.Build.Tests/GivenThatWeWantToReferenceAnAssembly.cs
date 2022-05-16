// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToReferenceAnAssembly : SdkTest
    {
        public GivenThatWeWantToReferenceAnAssembly(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netcoreapp1.0")]
        public void ItRunsAppsDirectlyReferencingAssemblies(
            string referencerTarget,
            string dependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString();

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return ""Hello from a direct reference."";
    }
}
";

            var dependencyAsset = _testAssetsManager.CreateTestProject(dependencyProject, identifier: identifier);
            string dependencyAssemblyPath = RestoreAndBuild(dependencyAsset, dependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.References.Add(dependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a direct reference.");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFramework)]
        public void ItRunsAppsDirectlyReferencingAssembliesWithSatellites(
            string referencerTarget,
            string dependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString();

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dependencyProject.SourceFiles["Class1.cs"] = @"
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

public class Class1
{
    public static string GetMessage()
    {
        CultureInfo.CurrentUICulture = new CultureInfo(""en-US"");
        var resources = new ResourceManager(""Dependency.Strings"", typeof(Class1).GetTypeInfo().Assembly);
        return resources.GetString(""HelloWorld"");
    }
}
";
            dependencyProject.EmbeddedResources["Strings.en.resx"] = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>1.3</value>
  </resheader>
  <resheader name=""reader"">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name=""writer"">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name=""HelloWorld"" xml:space=""preserve"">
    <value>Hello World from en satellite assembly for a direct reference.</value>
  </data>
</root>
";

            var dependencyAsset = _testAssetsManager.CreateTestProject(dependencyProject, identifier: identifier);
            string dependencyAssemblyPath = RestoreAndBuild(dependencyAsset, dependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.References.Add(dependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from en satellite assembly for a direct reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netcoreapp1.0")]
        public void ItRunsAppsDirectlyReferencingAssembliesWhichReferenceAssemblies(
            string referencerTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return ""Hello from a reference of an indirect reference."";
    }
}
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.References.Add(dllDependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a reference of an indirect reference.");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFramework)]
        public void ItRunsAppsDirectlyReferencingAssembliesWhichReferenceAssembliesWithSatellites(
            string referencerTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class2.cs"] = @"
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

public class Class2
{
    public static string GetMessage()
    {
        CultureInfo.CurrentUICulture = new CultureInfo(""en-US"");
        var resources = new ResourceManager(""DllDependencyDependency.Strings"", typeof(Class2).GetTypeInfo().Assembly);
        return resources.GetString(""HelloWorld"");
    }
}
";
            dllDependencyProjectDependency.EmbeddedResources["Strings.en.resx"] = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>1.3</value>
  </resheader>
  <resheader name=""reader"">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name=""writer"">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name=""HelloWorld"" xml:space=""preserve"">
    <value>Hello World from en satellite assembly for a reference of an indirect reference.</value>
  </data>
</root>
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.References.Add(dllDependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from en satellite assembly for a reference of an indirect reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netstandard2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netcoreapp1.0")]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssemblies(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return ""Hello from an indirect reference."";
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from an indirect reference.");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0", "netstandard2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0", ToolsetInfo.CurrentTargetFramework)]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssembliesWithSatellites(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

public class Class2
{
    public static string GetMessage()
    {
        CultureInfo.CurrentUICulture = new CultureInfo(""en-US"");
        var resources = new ResourceManager(""DllDependency.Strings"", typeof(Class2).GetTypeInfo().Assembly);
        return resources.GetString(""HelloWorld"");
    }
}
";
            dllDependencyProject.EmbeddedResources["Strings.en.resx"] = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>1.3</value>
  </resheader>
  <resheader name=""reader"">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name=""writer"">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name=""HelloWorld"" xml:space=""preserve"">
    <value>Hello World from en satellite assembly for an indirect reference.</value>
  </data>
</root>
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from en satellite assembly for an indirect reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netstandard2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netcoreapp1.0")]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssembliesWhichReferenceAssemblies(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class3.cs"] = @"
public class Class3
{
    public static string GetMessage()
    {
        return ""Hello from a reference of an indirect reference."";
    }
}
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return Class3.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a reference of an indirect reference.");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0", "netstandard2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "netstandard2.0", ToolsetInfo.CurrentTargetFramework)]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssembliesWhichReferenceAssembliesWithSatellites(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(referencerTarget))
            {
                return;
            }

            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class3.cs"] = @"
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

public class Class3
{
    public static string GetMessage()
    {
        CultureInfo.CurrentUICulture = new CultureInfo(""en-US"");
        var resources = new ResourceManager(""DllDependencyDependency.Strings"", typeof(Class3).GetTypeInfo().Assembly);
        return resources.GetString(""HelloWorld"");
    }
}
";
            dllDependencyProjectDependency.EmbeddedResources["Strings.en.resx"] = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>1.3</value>
  </resheader>
  <resheader name=""reader"">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name=""writer"">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name=""HelloWorld"" xml:space=""preserve"">
    <value>Hello World from en satellite assembly for a reference of an indirect reference.</value>
  </data>
</root>
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return Class3.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                TargetFrameworks = referencerTarget,
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            new DotnetCommand(Log, applicationPath)
                            .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from en satellite assembly for a reference of an indirect reference.");
        }

        private string RestoreAndBuild(TestAsset testAsset, TestProject testProject)
        {
            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(
                testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier);
            return Path.Combine(outputDirectory.FullName, testProject.Name + ".dll");
        }
    }
}
