// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class ProjectModification
    {
        public static void AddDisplayMessageToProject(XDocument project, string beforeTargets)
        {
            XNamespace ns = project.Root.Name.Namespace;

            XElement target = new XElement(ns + "Target", new XAttribute("Name", "DisplayMessages"),
                new XAttribute("BeforeTargets", beforeTargets));
            project.Root.Add(target);

            target.Add(new XElement(ns + "Message", new XAttribute("Text", "Important text"),
                new XAttribute("Importance", "high")));
        }

        public static void AddDisplayMessageBeforeVsTestToProject(XDocument project)
        {
            AddDisplayMessageToProject(project, "VSTest");
        }

        public static void AddDisplayMessageBeforeRestoreToProject(XDocument project)
        {
            AddDisplayMessageToProject(project, "Restore");
        }
    }
}
