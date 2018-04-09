// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    public sealed partial class RoslynCodeTaskFactory
    {
        private static readonly IDictionary<string, IDictionary<RoslynCodeTaskFactoryCodeType, string>> CodeTemplates = new Dictionary<string, IDictionary<RoslynCodeTaskFactoryCodeType, string>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "CS", new Dictionary<RoslynCodeTaskFactoryCodeType, string>
                {
                    {
                        RoslynCodeTaskFactoryCodeType.Fragment, @"{0}

namespace InlineCode
{{
    public class {1} : Microsoft.Build.Utilities.Task
    {{
        public bool Success {{ get; private set; }} = true;
{2}
        public override bool Execute()
        {{
            {3}
            return Success;
        }}
    }}
}}"
                    },
                    {
                        RoslynCodeTaskFactoryCodeType.Method, @"{0}

namespace InlineCode
{{
    public class {1} : Microsoft.Build.Utilities.Task
    {{
        public bool Success {{ get; private set; }} = true;
{2}
        {3}
    }}
}}"
                    },
                }
            },
            {
                "VB", new Dictionary<RoslynCodeTaskFactoryCodeType, string>
                {
                    {
                        RoslynCodeTaskFactoryCodeType.Fragment, @"{0}

Namespace InlineCode
    Public Class {1}
        Inherits Microsoft.Build.Utilities.Task

        Public Property Success As Boolean = True
{2}
        Public Overrides Function Execute() As Boolean
            {3}
            Return Success
        End Function

    End Class
End Namespace"
                    },
                    {
                        RoslynCodeTaskFactoryCodeType.Method, @"{0}

Namespace InlineCode
    Public Class {1}
        Inherits Microsoft.Build.Utilities.Task

        Public Property Success As Boolean = True
{2}
        {3}

    End Class
End Namespace"
                    }
                }
            }
        };
    }
}
