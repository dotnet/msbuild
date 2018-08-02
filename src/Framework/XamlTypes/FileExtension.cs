// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// simple class that deserialize extension to content type data
    /// </summary>
    public sealed class FileExtension : IProjectSchemaNode
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public FileExtension()
        {
        }

        /// <summary>
        /// file extension 
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// corresponding content type
        /// </summary>
        public string ContentType
        {
            get;
            set;
        }

        #region IProjectSchemaNode Members

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<Type> GetSchemaObjectTypes()
        {
            yield return typeof(FileExtension);
        }

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<object> GetSchemaObjects(Type type)
        {
            if (type == typeof(FileExtension))
            {
                yield return this;
            }
        }

        #endregion
    }
}
