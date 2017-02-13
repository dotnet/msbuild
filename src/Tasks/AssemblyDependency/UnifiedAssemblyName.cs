// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A unified assembly name.
    /// </summary>
    internal class UnifiedAssemblyName
    {
        private AssemblyNameExtension _preUnified;
        private AssemblyNameExtension _postUnified;
        private bool _isUnified;
        private bool _isPrerequisite;
        private bool? _isRedistRoot;
        private string _redistName;
        private UnificationReason _unificationReason;

        public UnifiedAssemblyName(AssemblyNameExtension preUnified, AssemblyNameExtension postUnified, bool isUnified, UnificationReason unificationReason, bool isPrerequisite, bool? isRedistRoot, string redistName)
        {
            _preUnified = preUnified;
            _postUnified = postUnified;
            _isUnified = isUnified;
            _isPrerequisite = isPrerequisite;
            _isRedistRoot = isRedistRoot;
            _redistName = redistName;
            _unificationReason = unificationReason;
        }

        public AssemblyNameExtension PreUnified
        {
            get { return _preUnified; }
        }

        public AssemblyNameExtension PostUnified
        {
            get { return _postUnified; }
        }

        public bool IsUnified
        {
            get { return _isUnified; }
        }

        public UnificationReason UnificationReason
        {
            get { return _unificationReason; }
        }

        public bool IsPrerequisite
        {
            get { return _isPrerequisite; }
        }

        public bool? IsRedistRoot
        {
            get { return _isRedistRoot; }
        }

        public string RedistName
        {
            get { return _redistName; }
        }
    }
}
