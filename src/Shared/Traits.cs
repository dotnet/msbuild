// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    ///     Represents toggleable features of the MSBuild engine
    /// </summary>
    internal class Traits
    {
        public static Traits Instance = new Traits();

        public EscapeHatches EscapeHatches { get; }

        public Traits()
        {
            EscapeHatches = new EscapeHatches();
        }
    }

    internal class EscapeHatches
    {
        public bool SerializeEntireProjectInstance => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDDONOTSERIALIZEENTIREPROJECTINSTANCE"));
    }
}
