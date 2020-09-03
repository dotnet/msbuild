// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Markup;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Used to deserialize the content type information 
    /// </summary>
    [ContentProperty("Metadata")]
    public sealed class ContentType : ISupportInitialize, IProjectSchemaNode
    {
        /// <summary>
        /// metadata hash
        /// </summary>
        private Lazy<Dictionary<string, string>> _metadata;

        /// <summary>
        /// Constructor
        /// </summary>
        public ContentType()
        {
            this.Metadata = new List<NameValuePair>();

            // We must use ExecutionAndPublication thread safety here because the initializer is a destructive operation.
            _metadata = new Lazy<Dictionary<string, string>>(this.InitializeMetadata, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// serializes IContentType.Name
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// serializes IContentType.DisplayName
        /// </summary>
        [Localizable(true)]
        public string DisplayName
        {
            get;
            set;
        }

        /// <summary>
        /// serializes IContentType.ItemType
        /// </summary>
        public string ItemType
        {
            get;
            set;
        }

        /// <summary>
        /// serializes IContentType.DefaultContentTypeForItemType
        /// </summary>
        public bool DefaultContentTypeForItemType
        {
            get;
            set;
        }

        /// <summary>
        /// This property was never used for anything.  It should have been removed before we shipped MSBuild 4.0.
        /// </summary>
        [Obsolete("Unused.  Use ItemType property instead.", true)]
        public string ItemGroupName
        {
            get;
            set;
        }

        /// <summary>
        /// serializes content type's metadata. Accessible via IContentType.GetMetadata()
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<NameValuePair> Metadata
        {
            get;
            set;
        }

        /// <summary>
        /// Access metadata in convenient way
        /// </summary>
        public string GetMetadata(string metadataName)
        {
            if (String.IsNullOrEmpty(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string value;
            _metadata.Value.TryGetValue(metadataName, out value);
            return value;
        }

        #region ISupportInitialize Members

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public void BeginInit()
        {
        }

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public void EndInit()
        {
        }

        #endregion

        #region IProjectSchemaNode Members

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<Type> GetSchemaObjectTypes()
        {
            yield return typeof(ContentType);
        }

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<object> GetSchemaObjects(Type type)
        {
            if (type == typeof(ContentType))
            {
                yield return this;
            }
        }

        #endregion

        /// <summary>
        /// Lazily initializes the metadata dictionary.
        /// </summary>
        /// <returns>The new dictionary.</returns>
        /// <remarks>
        /// This is a destructive operation.  It clears the NameValuePair list field.
        /// </remarks>
        private Dictionary<string, string> InitializeMetadata()
        {
            var metadata = new Dictionary<string, string>(this.Metadata.Count, StringComparer.OrdinalIgnoreCase);
            foreach (NameValuePair pair in this.Metadata)
            {
                metadata.Add(pair.Name, pair.Value);
            }

            return metadata;
        }
    }
}
