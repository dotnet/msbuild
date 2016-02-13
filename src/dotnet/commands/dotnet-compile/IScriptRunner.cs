// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler
{
    public interface IScriptRunner
    {
        void RunScripts(ProjectContext context, string name, Dictionary<string, string> contextVariables);
    }
}
