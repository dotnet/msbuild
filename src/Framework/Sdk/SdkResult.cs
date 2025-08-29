﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     An abstract interface class to indicate SDK resolver success or failure.
    /// </summary>
    /// <remarks>
    ///    <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!NOTE]
    /// > Use <xref:Microsoft.Build.Framework.SdkResultFactory> to create instances of this class. Do not inherit from this class.
    /// ]]></format>
    /// </remarks>
    public abstract class SdkResult
    {
        // Explicit backing fields so that implementation in Microsoft.Build.dll can use them for translation
        private protected bool _success;
        private protected string _path;
        private protected string _version;
        private protected IList<string> _additionalPaths;
        private protected IDictionary<string, string> _propertiesToAdd;
        private protected IDictionary<string, SdkResultItem> _itemsToAdd;
        private protected SdkReference _sdkReference;

        /// <summary>
        ///     Indicates the resolution was successful.
        /// </summary>
        public virtual bool Success { get => _success; protected set => _success = value; }

        /// <summary>
        ///     Resolved path to the SDK.
        /// 
        ///     Null if <see cref="Success"/> == false
        /// </summary>
        public virtual string Path { get => _path; protected set => _path = value; }

        /// <summary>
        ///     Resolved version of the SDK.
        ///     Can be null or empty if the resolver did not provide a version (e.g. a path based resolver)
        /// 
        ///     Null if <see cref="Success"/> == false
        /// </summary>
        public virtual string Version { get => _version; protected set => _version = value; }

        /// <summary>
        /// Additional resolved SDK paths beyond the one specified in <see cref="Path"/>
        /// </summary>
        /// <remarks>
        /// This allows an SDK resolver to return multiple SDK paths, which will all be imported.
        /// </remarks>
        public virtual IList<string> AdditionalPaths { get => _additionalPaths; set => _additionalPaths = value; }

        /// <summary>
        /// Properties that should be added to the evaluation.  This allows an SDK resolver to provide information to the build
        /// </summary>
        public virtual IDictionary<string, string> PropertiesToAdd { get => _propertiesToAdd; protected set => _propertiesToAdd = value; }

        /// <summary>
        /// Items that should be added to the evaluation.  This allows an SDK resolver to provide information to the build
        /// </summary>
        public virtual IDictionary<string, SdkResultItem> ItemsToAdd { get => _itemsToAdd; protected set => _itemsToAdd = value; }

        /// <summary>
        ///     The Sdk reference
        /// </summary>
        public virtual SdkReference SdkReference { get => _sdkReference; protected set => _sdkReference = value; }
    }
}
