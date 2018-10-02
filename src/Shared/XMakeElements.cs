// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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

        internal static readonly char[] InvalidTargetNameCharacters = { '$', '@', '(', ')', '%', '*', '?', '.' };

        // Names that cannot be used as property or item names because they are reserved
        internal static readonly HashSet<string> ReservedItemNames = new HashSet<string>
        {
            // XMakeElements.project, "Project" is not reserved, because unfortunately ProjectReference items already use it as metadata name.
            XMakeElements.visualStudioProject,
            XMakeElements.target,
            XMakeElements.propertyGroup,
            XMakeElements.output,
            XMakeElements.itemGroup,
            XMakeElements.usingTask,
            XMakeElements.projectExtensions,
            XMakeElements.onError,
            // XMakeElements.import "Import" items are used by Visual Basic projects
            XMakeElements.importGroup,
            XMakeElements.choose,
            XMakeElements.when,
            XMakeElements.otherwise
        };
    }
}
