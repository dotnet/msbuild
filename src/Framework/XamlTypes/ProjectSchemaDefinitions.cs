﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Markup;

#nullable disable

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Serialization class for node for the Data driven project schema XAML
    /// </summary>
    [ContentProperty("Nodes")]
    public sealed class ProjectSchemaDefinitions : IProjectSchemaNode
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ProjectSchemaDefinitions()
        {
            Nodes = new List<IProjectSchemaNode>();
        }

        /// <summary>
        /// Collection of any schema node
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<IProjectSchemaNode> Nodes
        {
            get;
            set;
        }

        #region IProjectSchemaNode Members
        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2301:EmbeddableTypesInContainersRule", MessageId = "allTypes", Justification = "All object types come from within this assembly, so there will not be any type equivalence problems")]
        public IEnumerable<Type> GetSchemaObjectTypes()
        {
            return Nodes.SelectMany(node => node.GetSchemaObjectTypes()).Distinct();
        }

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<object> GetSchemaObjects(Type type)
        {
            foreach (IProjectSchemaNode node in Nodes)
            {
                foreach (object o in node.GetSchemaObjects(type))
                {
                    yield return o;
                }
            }
        }
        #endregion
    }
}
