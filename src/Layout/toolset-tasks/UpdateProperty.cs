// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// MSBuild tasks that updates the value of property named <see cref="UpdateProperty.PropertyName"/> to <see cref="UpdateProperty.PropertyValue"/> in MSBuild file <see cref="UpdateProperty.FilePath'"/>.
    /// </summary>
    public class UpdateProperty : Task
    {
        /// <summary>
        /// The property to update. The property should exist in file and be defined only once.
        /// </summary>
        [Required]
        public string PropertyName { get; set; }

        /// <summary>
        /// The new value of the property.
        /// </summary>
        [Required]
        public string PropertyValue { get; set; }

        /// <summary>
        /// The path to MSBuild file to update.
        /// </summary>
        [Required]
        public string FilePath { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(FilePath))
            {
                Log.LogError("File {0} doesn't exist.", FilePath);
                return false;
            }
            XDocument projectXml;
            try
            {
                string projectFileContent = File.ReadAllText(FilePath);
                projectXml = XDocument.Parse(projectFileContent);
            }
            catch (Exception ex)
            {
                Log.LogError("File {0} is not a valid XML file, {1}", FilePath, ex.Message);
                return false;
            }

            XNamespace ns = projectXml.Root.Name.Namespace;
            IEnumerable<XElement> propertyGroups = projectXml.Root.Elements(ns + "PropertyGroup").Where(pg => pg.Elements(ns + PropertyName).Any());
            int propertyGroupsCount = propertyGroups.Count();

            if (propertyGroupsCount == 0)
            {
                Log.LogError("Property group with property '{0}' is not found.", PropertyName);
                return false;
            }

            if (propertyGroupsCount > 1)
            {
                Log.LogError("More than one property group with property '{0}' found.", PropertyName);
                return false;
            }

            XElement foundPropertyGroup = propertyGroups.Single();
            int elementsCount = foundPropertyGroup.Elements(ns + PropertyName).Count();

            if (elementsCount == 0)
            {
                Log.LogError("Property with name '{0}' is not found.", PropertyName);
                return false;
            }

            if (elementsCount > 1)
            {
                Log.LogError("More than one property with name '{0}' found.", PropertyName);
                return false;
            }

            foundPropertyGroup.Element(ns + PropertyName).Value = PropertyValue;

            try
            {
                File.WriteAllText(FilePath, projectXml.ToString());
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to update the content of file {0}, {1}", FilePath, ex.Message);
                return false;
            }

            return true;
        }

    }
}
