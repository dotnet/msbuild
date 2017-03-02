// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Wraps the &lt;runtime&gt; section of the .config file.
    /// </summary>
    internal sealed class RuntimeSection
    {
        /// <summary>
        /// List of dependent assemblies. Type is DependentAssembly.
        /// </summary>
        private ArrayList _dependentAssemblies = new ArrayList();

        /// <summary>
        /// The reader is positioned on a &lt;runtime&gt; element--read it.
        /// </summary>
        /// <param name="reader"></param>
        internal void Read(XmlReader reader)
        {
            while (reader.Read())
            {
                // Look for the end element.
                if (reader.NodeType == XmlNodeType.EndElement && AppConfig.StringEquals(reader.Name, "runtime"))
                {
                    return;
                }

                // Look for a <dependentAssembly> element
                if (reader.NodeType == XmlNodeType.Element && AppConfig.StringEquals(reader.Name, "dependentAssembly"))
                {
                    DependentAssembly dependentAssembly = new DependentAssembly();
                    dependentAssembly.Read(reader);

                    // Only add if there was an <assemblyIdentity> tag.
                    // Otherwise, this section is no use.
                    if (dependentAssembly.PartialAssemblyName != null)
                    {
                        _dependentAssemblies.Add(dependentAssembly);
                    }
                }
            }
        }

        /// <summary>
        /// Return the collection of dependent assemblies for this runtime element.
        /// </summary>
        /// <value></value>
        internal DependentAssembly[] DependentAssemblies
        {
            get { return (DependentAssembly[])_dependentAssemblies.ToArray(typeof(DependentAssembly)); }
        }
    }
}
