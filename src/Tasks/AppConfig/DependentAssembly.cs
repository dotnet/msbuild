// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a single &lt;dependentassembly&gt; from the app.config file.
    /// </summary>
    internal sealed class DependentAssembly
    {
        /// <summary>
        /// The partial <see cref="AssemblyName"/>, there should be no version.
        /// Setter and Getter clone the incoming / outgoing assembly
        /// </summary>
        internal AssemblyName PartialAssemblyName
        {
            set
            {
                AssemblyNameReadOnly = value.CloneIfPossible();
                AssemblyNameReadOnly.Version = null;
            }
            get => AssemblyNameReadOnly?.CloneIfPossible();
        }

        /// <summary>
        /// The full <see cref="AssemblyName"/>. It is not cloned. Callers should not mutate this object.
        /// </summary>
        internal AssemblyName AssemblyNameReadOnly { get; private set; }

        /// <summary>
        /// The reader is positioned on a &lt;dependentassembly&gt; element--read it.
        /// </summary>
        internal void Read(XmlReader reader)
        {
            var redirects = new List<BindingRedirect>();

            if (BindingRedirects != null)
            {
                redirects.AddRange(BindingRedirects);
            }

            while (reader.Read())
            {
                // Look for the end element.
                if (reader.NodeType == XmlNodeType.EndElement && AppConfig.StringEquals(reader.Name, "dependentassembly"))
                {
                    break;
                }

                // Look for a <assemblyIdentity> element
                if (reader.NodeType == XmlNodeType.Element && AppConfig.StringEquals(reader.Name, "assemblyIdentity"))
                {
                    string name = null;
                    string publicKeyToken = "null";
                    string culture = "neutral";

                    // App.config seems to have mixed case attributes.
                    while (reader.MoveToNextAttribute())
                    {
                        if (AppConfig.StringEquals(reader.Name, "name"))
                        {
                            name = reader.Value;
                        }
                        else
                        if (AppConfig.StringEquals(reader.Name, "publicKeyToken"))
                        {
                            publicKeyToken = reader.Value;
                        }
                        else
                        if (AppConfig.StringEquals(reader.Name, "culture"))
                        {
                            culture = reader.Value;
                        }
                    }

                    string assemblyName = String.Format
                    (
                        CultureInfo.InvariantCulture,
                        "{0}, Version=0.0.0.0, Culture={1}, PublicKeyToken={2}",
                        name,
                        culture,
                        publicKeyToken
                    );

                    try
                    {
                        AssemblyNameReadOnly = new AssemblyNameExtension(assemblyName).AssemblyName;
                    }
                    catch (System.IO.FileLoadException e)
                    {
                        // A badly formed assembly name.
                        ErrorUtilities.VerifyThrowArgument(false, e, "AppConfig.InvalidAssemblyIdentityFields");
                    }
                }

                // Look for a <bindingRedirect> element.
                if (reader.NodeType == XmlNodeType.Element && AppConfig.StringEquals(reader.Name, "bindingRedirect"))
                {
                    var bindingRedirect = new BindingRedirect();
                    bindingRedirect.Read(reader);
                    redirects.Add(bindingRedirect);
                }
            }
            BindingRedirects = redirects.ToArray();
        }

        /// <summary>
        /// The binding redirects.
        /// </summary>
        internal BindingRedirect[] BindingRedirects { set; get; }
    }
}
