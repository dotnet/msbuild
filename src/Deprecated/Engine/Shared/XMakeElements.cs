// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Contains the names of the known elements in the XML project file.
    /// </summary>
    /// <owner>RGoel</owner>
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

        /// <summary>
        /// Indicates if the given node is valid as a child of a task element.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="childNode"></param>
        /// <returns>true, if specified node can be a child of a task element</returns>
        internal static bool IsValidTaskChildNode(XmlNode childNode)
        {
            return (childNode.Name == output) ||
                    (childNode.NodeType == XmlNodeType.Comment) ||
                    (childNode.NodeType == XmlNodeType.Whitespace);
        }

        internal static readonly char[] illegalTargetNameCharacters = new char[] { '$', '@', '(', ')', '%', '*', '?', '.' };

        // Names that cannot be used as property or item names because they are reserved
        internal static readonly string[] illegalPropertyOrItemNames = new string[] { 
//            XMakeElements.project, // "Project" is not reserved, because unfortunately ProjectReference items 
                                     // already use it as metadata name.
            XMakeElements.visualStudioProject,
            XMakeElements.target,
            XMakeElements.propertyGroup,
            XMakeElements.output,
            XMakeElements.itemGroup,
            XMakeElements.usingTask,
            XMakeElements.projectExtensions,
            XMakeElements.onError,
//            XMakeElements.import, // "Import" items are used by Visual Basic projects
            XMakeElements.importGroup,
            XMakeElements.choose,
            XMakeElements.when,
            XMakeElements.otherwise
        };

        // The set of XMake reserved item/property names (e.g. Choose, Message etc.)
        private static Hashtable illegalItemOrPropertyNamesHashtable;

        /// <summary>
        /// Read-only internal accessor for the hashtable containing
        /// MSBuild reserved item/property names (like "Choose", for example).
        /// </summary>
        /// <owner>LukaszG</owner>
        internal static Hashtable IllegalItemPropertyNames
        {
            get
            {
                // Lazy creation
                if (illegalItemOrPropertyNamesHashtable == null)
                {
                    illegalItemOrPropertyNamesHashtable = new Hashtable(XMakeElements.illegalPropertyOrItemNames.Length);

                    foreach (string reservedName in XMakeElements.illegalPropertyOrItemNames)
                    {
                        illegalItemOrPropertyNamesHashtable.Add(reservedName, string.Empty);
                    }
                }

                return illegalItemOrPropertyNamesHashtable;
            }
        }
    }
}
