// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System;
using Microsoft.Build.Shared;

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
        public ProjectInstanceTranslationMode? ProjectInstanceTranslation => new Lazy<ProjectInstanceTranslationMode?>(
            () =>
            {
                var mode = Environment.GetEnvironmentVariable("MSBUILD_PROJECTINSTANCE_TRANSLATION_MODE");

                if (mode == null)
                {
                    return null;
                }

                if (mode.Equals("full", StringComparison.OrdinalIgnoreCase))
                {
                    return ProjectInstanceTranslationMode.Full;
                }

                if (mode.Equals("partial", StringComparison.OrdinalIgnoreCase))
                {
                    return ProjectInstanceTranslationMode.Partial;
                }

                ErrorUtilities.ThrowInvalidOperation("Shared.InvalidEscapeHatchValue", mode);

                return null;
            },
            true).Value;

        public enum ProjectInstanceTranslationMode
        {
            Full,
            Partial
        }
    }
}
