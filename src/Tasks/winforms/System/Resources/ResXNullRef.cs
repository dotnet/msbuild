// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {
    using System.Diagnostics;

    using System;
    using System.Windows.Forms;
    using System.Reflection;
    using Microsoft.Win32;
    using System.Drawing;
    using System.IO;
    using System.ComponentModel;
    using System.Collections;
    using System.Resources;
    using System.Globalization;

    /// <include file='doc\ResXNullRef.uex' path='docs/doc[@for="ResXNullRef"]/*' />
    /// <devdoc>
    ///     ResX Null Reference class.  This class allows ResX to store null values.
    ///     It is a placeholder that is written into the file.  On read, it is replaced
    ///     with null.
    /// </devdoc>
    [Serializable]
    internal sealed class ResXNullRef {
    }
}

