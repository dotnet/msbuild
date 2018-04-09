// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class RoslynCodeTaskFactory_Tests
    {
        private const string TaskName = "MyInlineTask";

        [Fact]
        public void ApplySourceCodeTemplateVisualBasicFragment()
        {
            const string fragment = "Dim x = 0";
            string expectedSourceCode = $@"Imports Microsoft.Build.Framework
Imports Microsoft.Build.Utilities
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace InlineCode
    Public Class {TaskName}
        Inherits Microsoft.Build.Utilities.Task

        Public Property Success As Boolean = True

        Public Overrides Function Execute() As Boolean
            {fragment}
            Return Success
        End Function

    End Class
End Namespace";

            TryLoadTaskBodyAndExpectSuccess(
                taskBody: $"<Code Language=\"VB\">{fragment}</Code>",
                expectedCodeLanguage: "VB",
                expectedSourceCode: expectedSourceCode,
                expectedCodeType: RoslynCodeTaskFactoryCodeType.Fragment);
        }

        [Fact]
        public void ApplySourceCodeTemplateVisualBasicFragmentWithProperties()
        {
            ICollection<TaskPropertyInfo> parameters = new List<TaskPropertyInfo>
            {
                new TaskPropertyInfo("Parameter1", typeof(string), output: false, required: true),
                new TaskPropertyInfo("Parameter2", typeof(string), output: true, required: false),
                new TaskPropertyInfo("Parameter3", typeof(string), output: true, required: true),
                new TaskPropertyInfo("Parameter4", typeof(ITaskItem), output: false, required: false),
                new TaskPropertyInfo("Parameter5", typeof(ITaskItem[]), output: false, required: false),
            };

            const string fragment = @"int x = 0;";

            string expectedSourceCode = $@"Imports Microsoft.Build.Framework
Imports Microsoft.Build.Utilities
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace InlineCode
    Public Class {TaskName}
        Inherits Microsoft.Build.Utilities.Task

        Public Property Success As Boolean = True
        <Microsoft.Build.Framework.RequiredAttribute>
        Public Property Parameter1 As System.String
        <Microsoft.Build.Framework.OutputAttribute>
        Public Property Parameter2 As System.String
        <Microsoft.Build.Framework.OutputAttribute>
        <Microsoft.Build.Framework.RequiredAttribute>
        Public Property Parameter3 As System.String
        Public Property Parameter4 As Microsoft.Build.Framework.ITaskItem
        Public Property Parameter5 As Microsoft.Build.Framework.ITaskItem[]
        Public Overrides Function Execute() As Boolean
            {fragment}
            Return Success
        End Function

    End Class
End Namespace";

            TryLoadTaskBodyAndExpectSuccess(
                taskBody: $"<Code Language=\"VB\">{fragment}</Code>",
                expectedCodeLanguage: "VB",
                expectedSourceCode: expectedSourceCode,
                expectedCodeType: RoslynCodeTaskFactoryCodeType.Fragment,
                parameters: parameters);
        }

        [Fact]
        public void ApplySourceCodeTemplateVisualBasicMethod()
        {
            const string method = @"Public Overrides Function Execute() As Boolean\r
            Dim x = 0
            Return Success
        End Function";

            string expectedSourceCode = $@"Imports Microsoft.Build.Framework
Imports Microsoft.Build.Utilities
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace InlineCode
    Public Class {TaskName}
        Inherits Microsoft.Build.Utilities.Task

        Public Property Success As Boolean = True

        {method.Replace("\r", "")}

    End Class
End Namespace";
            TryLoadTaskBodyAndExpectSuccess(
                taskBody: $"<Code Language=\"VB\" Type=\"Method\">{method}</Code>",
                expectedCodeLanguage: "VB",
                expectedSourceCode: expectedSourceCode,
                expectedCodeType: RoslynCodeTaskFactoryCodeType.Method);
        }

        [Fact]
        public void CodeLanguageFromTaskBody()
        {
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"CS\">code</Code>", expectedCodeLanguage: "CS");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"cs\">code</Code>", expectedCodeLanguage: "CS");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"csharp\">code</Code>", expectedCodeLanguage: "CS");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"c#\">code</Code>", expectedCodeLanguage: "CS");

            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"VB\">code</Code>", expectedCodeLanguage: "VB");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"vb\">code</Code>", expectedCodeLanguage: "VB");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"visualbasic\">code</Code>", expectedCodeLanguage: "VB");
            TryLoadTaskBodyAndExpectSuccess("<Code Language=\"ViSuAl BaSic\">code</Code>", expectedCodeLanguage: "VB");
        }
/*
        TODO: Port the code
        [Fact]
        public void CodeTypeClassIgnoresParameterGroupWarning()
        {
            TryLoadTaskBodyAndExpectSuccess(
                taskBody: "<Code Type=\"Class\">code</Code>",
                parameters: new[]
                {
                    new TaskPropertyInfo("P", typeof(string), false, false),
                },
                expectedWarningMessages: new[]
                {
                    "Parameters are discovered through reflection for Type=\"Class\". Values specified in <ParameterGroup/> will be ignored.",
                });
        }
*/
        [Fact]
        public void CodeTypeFromTaskBody()
        {
            foreach (RoslynCodeTaskFactoryCodeType codeType in Enum.GetValues(typeof(RoslynCodeTaskFactoryCodeType)).Cast<RoslynCodeTaskFactoryCodeType>())
            {
                TryLoadTaskBodyAndExpectSuccess($"<Code Type=\"{codeType}\">code</Code>", expectedCodeType: codeType);
            }

            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFile file = testEnvironment.CreateFile(fileName: "236D48CE30064161B31B55DBF088C8B2", contents: "6159BD98607A460AA4F11D2FA92E5436");

                TryLoadTaskBodyAndExpectSuccess($"<Code Source=\"{file.Path}\"/>", expectedCodeType: RoslynCodeTaskFactoryCodeType.Class);
            }
        }

        [Fact]
        public void CSharpFragment()
        {
            const string fragment = "int x = 0;";
            string expectedSourceCode = $@"using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InlineCode
{{
    public class {TaskName} : Microsoft.Build.Utilities.Task
    {{
        public bool Success {{ get; private set; }} = true;

        public override bool Execute()
        {{
            {fragment}
            return Success;
        }}
    }}
}}";
            TryLoadTaskBodyAndExpectSuccess(taskBody: $"<Code>{fragment}</Code>", expectedSourceCode: expectedSourceCode);
        }

        [Fact]
        public void CSharpFragmentWithProperties()
        {
            ICollection<TaskPropertyInfo> parameters = new List<TaskPropertyInfo>
            {
                new TaskPropertyInfo("Parameter1", typeof(string), output: false, required: true),
                new TaskPropertyInfo("Parameter2", typeof(string), output: true, required: false),
                new TaskPropertyInfo("Parameter3", typeof(string), output: true, required: true),
                new TaskPropertyInfo("Parameter4", typeof(ITaskItem), output: false, required: false),
                new TaskPropertyInfo("Parameter5", typeof(ITaskItem[]), output: false, required: false),
            };

            const string fragment = @"int x = 0;";

            string expectedSourceCode = $@"using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InlineCode
{{
    public class {TaskName} : Microsoft.Build.Utilities.Task
    {{
        public bool Success {{ get; private set; }} = true;
        [Microsoft.Build.Framework.RequiredAttribute]
        public System.String Parameter1 {{ get; set; }}
        [Microsoft.Build.Framework.OutputAttribute]
        public System.String Parameter2 {{ get; set; }}
        [Microsoft.Build.Framework.OutputAttribute]
        [Microsoft.Build.Framework.RequiredAttribute]
        public System.String Parameter3 {{ get; set; }}
        public Microsoft.Build.Framework.ITaskItem Parameter4 {{ get; set; }}
        public Microsoft.Build.Framework.ITaskItem[] Parameter5 {{ get; set; }}
        public override bool Execute()
        {{
            {fragment}
            return Success;
        }}
    }}
}}";

            TryLoadTaskBodyAndExpectSuccess(
                taskBody: $"<Code>{fragment}</Code>",
                expectedSourceCode: expectedSourceCode,
                expectedCodeType: RoslynCodeTaskFactoryCodeType.Fragment,
                parameters: parameters);
        }

        [Fact]
        public void CSharpMethod()
        {
            const string method = @"public override bool Execute() { int x = 0; return Success; }";

            string expectedSourceCode = $@"using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InlineCode
{{
    public class {TaskName} : Microsoft.Build.Utilities.Task
    {{
        public bool Success {{ get; private set; }} = true;

        {method}
    }}
}}";
            TryLoadTaskBodyAndExpectSuccess(
                taskBody: $"<Code Type=\"Method\">{method}</Code>",
                expectedSourceCode: expectedSourceCode,
                expectedCodeType: RoslynCodeTaskFactoryCodeType.Method);
        }

        [Fact]
        public void EmptyCodeElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code />",
                expectedErrorMessage: "You must specify source code within the Code element or a path to a file containing source code.");
        }

        [Fact]
        public void EmptyIncludeAttributeOnReferenceElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Reference Include=\"\" />",
                expectedErrorMessage: "The \"Include\" attribute of the <Reference> element has been set but is empty. If the \"Include\" attribute is set it must not be empty.");
        }

        [Fact]
        public void EmptyLanguageAttributeOnCodeElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Language=\"\" />",
                expectedErrorMessage: "The \"Language\" attribute of the <Code> element has been set but is empty. If the \"Language\" attribute is set it must not be empty.");
        }

        [Fact]
        public void EmptyNamespaceAttributeOnUsingElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Using Namespace=\"\" />",
                expectedErrorMessage: "The \"Namespace\" attribute of the <Using> element has been set but is empty. If the \"Namespace\" attribute is set it must not be empty.");
        }

        [Fact]
        public void EmptySourceAttributeOnCodeElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Source=\"\" />",
                expectedErrorMessage: "The \"Source\" attribute of the <Code> element has been set but is empty. If the \"Source\" attribute is set it must not be empty.");
        }

        [Fact]
        public void EmptyTypeAttributeOnCodeElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Type=\"\" />",
                expectedErrorMessage: "The \"Type\" attribute of the <Code> element has been set but is empty. If the \"Type\" attribute is set it must not be empty.");
        }

        [Fact]
        public void IgnoreTaskCommentsAndWhiteSpace()
        {
            TryLoadTaskBodyAndExpectSuccess("<!-- Comment --><Code>code</Code>");
            TryLoadTaskBodyAndExpectSuccess("                <Code>code</Code>");
        }

        [Fact]
        public void InvalidCodeElementAttribute()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Invalid=\"Attribute\" />",
                expectedErrorMessage: "The attribute \"Invalid\" is not valid for the <Code> element.  Valid attributes are \"Language\", \"Source\", and \"Type\".");
        }

        [Fact]
        public void InvalidCodeLanguage()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Language=\"Invalid\" />",
                expectedErrorMessage: "The specified code language \"Invalid\" is invalid.  The supported code languages are \"CS, VB\".");
        }

        [Fact]
        public void InvalidCodeType()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code Type=\"Invalid\" />",
                expectedErrorMessage: "The specified code type \"Invalid\" is invalid.  The supported code types are \"Fragment, Method, Class\".");
        }

        [Fact]
        public void InvalidTaskChildElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Invalid />",
                expectedErrorMessage: "The element <Invalid> is not a valid child of the <Task> element.  Valid child elements are <Code>, <Reference>, and <Using>.");

            TryLoadTaskBodyAndExpectFailure(
                taskBody: "invalid<Code>code</Code>",
                expectedErrorMessage: "The element <Text> is not a valid child of the <Task> element.  Valid child elements are <Code>, <Reference>, and <Using>.");
        }

        [Fact]
        public void InvalidTaskXml()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<invalid xml",
                expectedErrorMessage: "The specified task XML is invalid.  '<' is an unexpected token. The expected token is '='. Line 1, position 19.");
        }

        [Fact]
        public void MissingCodeElement()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "",
                expectedErrorMessage: $"The <Code> element is missing for the \"{TaskName}\" task. This element is required.");
        }

        [Fact]
        public void MultipleCodeNodes()
        {
            TryLoadTaskBodyAndExpectFailure(
                taskBody: "<Code><![CDATA[]]></Code><Code></Code>",
                expectedErrorMessage: "Only one <Code> element can be specified.");
        }

        [Fact]
        public void NamespacesFromTaskBody()
        {
            const string taskBody = @"
                <Using Namespace=""namespace.A"" />
                <Using Namespace=""   namespace.B   "" />
                <Using Namespace=""namespace.C""></Using>
                <Code>code</Code>";

            TryLoadTaskBodyAndExpectSuccess(
                taskBody,
                expectedNamespaces: new HashSet<string>
                {
                    "namespace.A",
                    "namespace.B",
                    "namespace.C",
                });
        }

        [Fact]
        public void ReferencesFromTaskBody()
        {
            const string taskBody = @"
                <Reference Include=""AssemblyA"" />
                <Reference Include=""   AssemblyB   "" />
                <Reference Include=""AssemblyC""></Reference>
                <Reference Include=""C:\Program Files(x86)\Common Files\Microsoft\AssemblyD.dll"" />
                <Code>code</Code>";

            TryLoadTaskBodyAndExpectSuccess(
                taskBody,
                expectedReferences: new HashSet<string>
                {
                    "AssemblyA",
                    "AssemblyB",
                    "AssemblyC",
                    @"C:\Program Files(x86)\Common Files\Microsoft\AssemblyD.dll"
                });
        }

        [Fact]
        public void SourceCodeFromFile()
        {
            const string sourceCodeFileContents = @"
1F214E27A13F432B9397F1733BC55929
9111DC29B0064E6994A68CFE465404D4";

            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFile file = testEnvironment.CreateFile(fileName: "CB3096DA4A454768AA9C0C4D422FC188.tmp", contents: sourceCodeFileContents);

                TryLoadTaskBodyAndExpectSuccess(
                    $"<Code Source=\"{file.Path}\"/>",
                    expectedSourceCode: sourceCodeFileContents,
                    expectedCodeType: RoslynCodeTaskFactoryCodeType.Class);
            }
        }

        private void TryLoadTaskBodyAndExpectFailure(string taskBody, string expectedErrorMessage)
        {
            if (expectedErrorMessage == null)
            {
                throw new ArgumentNullException(nameof(expectedErrorMessage));
            }

            MockEngine buildEngine = new MockEngine();

            TaskLoggingHelper log = new TaskLoggingHelper(buildEngine, TaskName)
            {
                TaskResources = Shared.AssemblyResources.PrimaryResources
            };

            bool success = RoslynCodeTaskFactory.TryLoadTaskBody(log, TaskName, taskBody, new List<TaskPropertyInfo>(), out RoslynCodeTaskFactoryTaskInfo _);

            success.ShouldBeFalse();

            buildEngine.Errors.ShouldBe(1);

            buildEngine.Log.ShouldContain(expectedErrorMessage, () => buildEngine.Log);
        }

        private void TryLoadTaskBodyAndExpectSuccess(
            string taskBody,
            ICollection<TaskPropertyInfo> parameters = null,
            ISet<string> expectedReferences = null,
            ISet<string> expectedNamespaces = null,
            string expectedCodeLanguage = null,
            RoslynCodeTaskFactoryCodeType? expectedCodeType = null,
            string expectedSourceCode = null,
            IReadOnlyList<string> expectedWarningMessages = null)
        {
            MockEngine buildEngine = new MockEngine();
            

            TaskLoggingHelper log = new TaskLoggingHelper(buildEngine, TaskName)
            {
                TaskResources = Shared.AssemblyResources.PrimaryResources
            };

            bool success = RoslynCodeTaskFactory.TryLoadTaskBody(log, TaskName, taskBody, parameters ?? new List<TaskPropertyInfo>(), out RoslynCodeTaskFactoryTaskInfo taskInfo);

            buildEngine.Errors.ShouldBe(0, buildEngine.Log);

            if (expectedWarningMessages == null)
            {
                buildEngine.Warnings.ShouldBe(0);
            }
            else
            {
                string output = buildEngine.Log;

                foreach (string expectedWarningMessage in expectedWarningMessages)
                {
                    output.ShouldContain(expectedWarningMessage, () => output);
                }
            }

            success.ShouldBeTrue();

            if (expectedReferences != null)
            {
                taskInfo.References.ShouldBe(expectedReferences);
            }

            if (expectedNamespaces != null)
            {
                taskInfo.Namespaces.ShouldBe(expectedNamespaces);
            }

            if (expectedCodeLanguage != null)
            {
                taskInfo.CodeLanguage.ShouldBe(expectedCodeLanguage);
            }

            if (expectedCodeType != null)
            {
                taskInfo.CodeType.ShouldBe(expectedCodeType.Value);
            }

            if (expectedSourceCode != null)
            {
                taskInfo.SourceCode.ShouldBe(expectedSourceCode, StringCompareShould.IgnoreLineEndings);
            }
        }
    }
}
