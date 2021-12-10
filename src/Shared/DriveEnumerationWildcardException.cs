// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Thrown in cases where a drive enumeration wildcard was encountered when finding files that match a given file spec.
    /// </summary>
    internal class DriveEnumerationWildcardException : Exception
    {
        public DriveEnumerationWildcardException()
        {
        }

        public DriveEnumerationWildcardException(string projectDirectory, string wildcardEvaluation)
            : base(ConstructErrorMessage(projectDirectory, wildcardEvaluation))
        {
        }

        public DriveEnumerationWildcardException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private static string ConstructErrorMessage(string projectDirectory, string wildcardEvaluation)
        {
            return $"Failed to find files in {projectDirectory} that matched the filespec {wildcardEvaluation}, " +
                    "as this resulted in an attempted drive enumeration. Ensure that items and properties " +
                    "are properly defined in your project.";
        }
    }
}
