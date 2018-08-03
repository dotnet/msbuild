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
    /// This is a simple container for <see cref="Rule"/> instances. 
    /// </summary>
    /// <remarks>
    /// Note that we only deal in terms of <see cref="Rule"/>s
    /// as far as property pages are concerned. The <see cref="RuleBag"/> is only used as a 
    /// container for more than one <see cref="Rule"/>. The containing <see cref="Rule"/>s are 
    /// immediately stripped off after loading of the xaml file.
    /// </remarks>
    [ContentProperty("Rules")]
    public sealed class RuleBag : ISupportInitialize, IProjectSchemaNode
    {
        #region Constructor

        /// <summary>
        /// Default constructor needed for XAML deserialization.
        /// </summary>
        public RuleBag()
        {
            Rules = new List<Rule>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The collection of <see cref="Rule"/> instances this <see cref="RuleBag"/> instance contains.
        /// Must have at least one <see cref="Rule"/>.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<Rule> Rules
        {
            get;
            set;
        }

        #endregion 

        #region ISupportInitialize Members

        /// <summary>
        /// See ISupportInitialize Members.
        /// </summary>
        public void BeginInit()
        {
        }

        /// <summary>
        /// See ISupportInitialize Members.
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
            yield return typeof(Rule);
        }

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<object> GetSchemaObjects(Type type)
        {
            if (type == typeof(Rule))
            {
                foreach (Rule r in Rules)
                {
                    yield return r;
                }
            }
        }
        #endregion
    }
}
