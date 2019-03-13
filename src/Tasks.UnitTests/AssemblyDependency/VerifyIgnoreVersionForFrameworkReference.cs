using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework
    /// </summary>
    public sealed class VerifyIgnoreVersionForFrameworkReference : ResolveAssemblyReferenceTestFixture
    {
        public VerifyIgnoreVersionForFrameworkReference(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionBasic()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.IgnoreVersionForFrameworkReferences = true;
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);


            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."

            // Do the resolution without the metadata, expect it to not work since we should not be able to find Dependson9 version 10.0.0.0
            e = new MockEngine(_output);

            item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            items = new ITaskItem[]
            {
                item
            };

            redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                           "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                           "</FileList >";

            t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionBasicTestMetadata()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");


            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."

            // Do the resolution without the metadata, expect it to not work since we should not be able to find Dependson9 version 10.0.0.0
            e = new MockEngine(_output);

            item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            items = new ITaskItem[]
            {
                item
            };

            redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                           "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                           "</FileList >";

            t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionDisableIfSpecificVersionTrue()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");
            item.SetMetadata("SpecificVersion", "True");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='DependsOn9' Version='2.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionDisableIfHintPath()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");
            item.SetMetadata("HintPath", Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"));

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='DependsOn9' Version='2.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);


            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Empty(t.ResolvedFiles);
        }
    }
}
