// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Interface that we expect all root classes from project schema XAML files to implement
    /// </summary>
    public interface IProjectSchemaNode
    {
        /// <summary>
        /// Return all types of static data for data driven features this node contains
        /// </summary>
        IEnumerable<Type> GetSchemaObjectTypes();

        /// <summary>
        /// Returns all instances of static data with Type "type".  Null or Empty list if there is no objects from asked type provided by this node
        /// </summary>
        IEnumerable<object> GetSchemaObjects(Type type);
    }
}
