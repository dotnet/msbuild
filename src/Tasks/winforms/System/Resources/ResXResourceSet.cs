// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {

    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    using System;
    using System.Windows.Forms;
    using System.Reflection;
    using Microsoft.Win32;
    using System.Drawing;
    using System.IO;
    using System.ComponentModel;
    using System.Collections;
    using System.Resources;

    /// <include file='doc\ResXResourceSet.uex' path='docs/doc[@for="ResXResourceSet"]/*' />
    /// <devdoc>
    ///     ResX resource set.
    /// </devdoc>
    internal class ResXResourceSet : ResourceSet {

        /// <include file='doc\ResXResourceSet.uex' path='docs/doc[@for="ResXResourceSet.ResXResourceSet"]/*' />
        /// <devdoc>
        ///     Creates a resource set for the specified file.
        /// </devdoc>
        [
            SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")  // Shipped like this in Everett.
        ]
        public ResXResourceSet(string fileName) : base(new ResXResourceReader(fileName)) {
            ReadResources();
        }

        /// <include file='doc\ResXResourceSet.uex' path='docs/doc[@for="ResXResourceSet.ResXResourceSet1"]/*' />
        /// <devdoc>
        ///     Creates a resource set for the specified stream.
        /// </devdoc>
        [
            SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")  // Shipped like this in Everett.
        ]
        public ResXResourceSet(Stream stream) : base(new ResXResourceReader(stream)) {
            ReadResources();
        }

        /// <include file='doc\ResXResourceSet.uex' path='docs/doc[@for="ResXResourceSet.GetDefaultReader"]/*' />
        /// <devdoc>
        ///     Gets the default reader type associated with this set.
        /// </devdoc>
        public override Type GetDefaultReader() {
            return typeof(ResXResourceReader);
        }

        /// <include file='doc\ResXResourceSet.uex' path='docs/doc[@for="ResXResourceSet.GetDefaultWriter"]/*' />
        /// <devdoc>
        ///     Gets the default writer type associated with this set.
        /// </devdoc>
        public override Type GetDefaultWriter() {
            return typeof(ResXResourceWriter);
        }
    }
}
