// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    internal static class ProjectExtensions
    {
        private static readonly KeyValuePair<string, string>[] _compilerNameToLanguageId =
        {
            new KeyValuePair<string, string>("csc", "cs"),
            new KeyValuePair<string, string>("vbc", "vb"),
            new KeyValuePair<string, string>("fsc", "fs")
        };

        public static string GetSourceCodeLanguage(this Project project)
        {
            foreach (var kvp in _compilerNameToLanguageId)
            {
                if (kvp.Key == (project._defaultCompilerOptions.CompilerName))
                {
                    return kvp.Value;
                }
            }
            return null;
        }
    }
}
