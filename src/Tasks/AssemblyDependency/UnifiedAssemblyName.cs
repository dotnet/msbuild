// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A unified assembly name.
    /// </summary>
    internal class UnifiedAssemblyName
    {
        public UnifiedAssemblyName(AssemblyNameExtension preUnified, AssemblyNameExtension postUnified, bool isUnified, UnificationReason unificationReason, bool isPrerequisite, bool? isRedistRoot, string redistName)
        {
            PreUnified = preUnified;
            PostUnified = postUnified;
            IsUnified = isUnified;
            IsPrerequisite = isPrerequisite;
            IsRedistRoot = isRedistRoot;
            RedistName = redistName;
            UnificationReason = unificationReason;
        }

        public AssemblyNameExtension PreUnified { get; }

        public AssemblyNameExtension PostUnified { get; }

        public bool IsUnified { get; }

        public UnificationReason UnificationReason { get; }

        public bool IsPrerequisite { get; }

        public bool? IsRedistRoot { get; }

        public string RedistName { get; }
    }
}
