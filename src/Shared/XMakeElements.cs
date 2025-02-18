// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Contains the names of the known elements in the XML project file.
    /// </summary>
    internal static class XMakeElements
    {
        internal const string project = "Project";
        internal const string visualStudioProject = "VisualStudioProject";
        internal const string target = "Target";
        internal const string propertyGroup = "PropertyGroup";
        internal const string output = "Output";
        internal const string itemGroup = "ItemGroup";
        internal const string itemDefinitionGroup = "ItemDefinitionGroup";
        internal const string usingTask = "UsingTask";
        internal const string projectExtensions = "ProjectExtensions";
        internal const string onError = "OnError";
        internal const string error = "Error";
        internal const string warning = "Warning";
        internal const string message = "Message";
        internal const string import = "Import";
        internal const string importGroup = "ImportGroup";
        internal const string choose = "Choose";
        internal const string when = "When";
        internal const string otherwise = "Otherwise";
        internal const string usingTaskParameterGroup = "ParameterGroup";
        internal const string usingTaskParameter = "Parameter";
        internal const string usingTaskBody = "Task";
        internal const string sdk = "Sdk";

#if NET
        internal static readonly SearchValues<char> InvalidTargetNameCharacters = SearchValues.Create(
#else
        internal static readonly char[] InvalidTargetNameCharacters = (
#endif
            ['$', '@', '(', ')', '%', '*', '?', '.']);

        // Names that cannot be used as property or item names because they are reserved
        internal static readonly HashSet<string> ReservedItemNames =
        [
            // project, "Project" is not reserved, because unfortunately ProjectReference items already use it as metadata name.
            visualStudioProject,
            target,
            propertyGroup,
            output,
            itemGroup,
            usingTask,
            projectExtensions,
            onError,
            // import, "Import" items are used by Visual Basic projects
            importGroup,
            choose,
            when,
            otherwise,
        ];
    }
}
