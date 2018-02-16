// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging messages with different levels of importance from the SDK targets.
    /// </summary>
    public class NETSdkMessage : NETSdkBaseMessage
    {
        public string Importance
        {
            get
            {
                return MessageImportance;
            }

            set
            {
                MessageImportance = value;
            }
        }

        protected override string Severity => "Info";
    }
}
