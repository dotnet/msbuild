// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Reflection;
using System.Collections;
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
        /// List of binding redirects. Type is BindingRedirect.
        /// </summary>
        private BindingRedirect[] _bindingRedirects = null;

        /// <summary>
        /// The partial <see cref="AssemblyName"/>, there should be no version.
        /// </summary>
        private AssemblyName _partialAssemblyName;

        /// <summary>
        /// The partial <see cref="AssemblyName"/>, there should be no version.
        /// </summary>
        internal AssemblyName PartialAssemblyName
        {
            set
            {
                _partialAssemblyName = value.CloneIfPossible();
                _partialAssemblyName.Version = null;
            }
            get
            {
                return _partialAssemblyName?.CloneIfPossible();
            }
        }

        /// <summary>
        /// The reader is positioned on a &lt;dependentassembly&gt; element--read it.
        /// </summary>
        /// <param name="reader"></param>
        internal void Read(XmlReader reader)
        {
            ArrayList redirects = new ArrayList();

            if (_bindingRedirects != null)
            {
                redirects.AddRange(_bindingRedirects);
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
                        _partialAssemblyName = new AssemblyNameExtension(assemblyName).AssemblyName;
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
                    BindingRedirect bindingRedirect = new BindingRedirect();
                    bindingRedirect.Read(reader);
                    redirects.Add(bindingRedirect);
                }
            }
            _bindingRedirects = (BindingRedirect[])redirects.ToArray(typeof(BindingRedirect));
        }

        /// <summary>
        /// The binding redirects.
        /// </summary>
        /// <value></value>
        internal BindingRedirect[] BindingRedirects
        {
            set { _bindingRedirects = value; }
            get { return _bindingRedirects; }
        }
    }
}
