// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Runtime.CompilerServices;

// Type-forward to the inbox class where available, in order to maintain binary compatibility
// between the .NET and .NET Standard 2.0 assemblies.
[assembly: TypeForwardedTo(typeof(IsExternalInit))]

#else

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    // Needed so we can use init setters in full fw or netstandard
    //  (details: https://developercommunity.visualstudio.com/t/error-cs0518-predefined-type-systemruntimecompiler/1244809)
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
