// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {

    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System;
    using System.Reflection;
    /// <include file='doc\IAliasResolver.uex' path='docs/doc[@for="IAliasResolver"]/*' />
    /// <devdoc>
    /// Summary of IAliasResolver.
    /// </devdoc>
    internal interface IAliasResolver {
        AssemblyName ResolveAlias(string alias);
        void PushAlias(string alias, AssemblyName name);
    }
}

