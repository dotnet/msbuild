// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal static partial class MigrationErrorCodes
    {
        public static Func<string, MigrationError> MIGRATE1011
            => (message) => new MigrationError(nameof(MIGRATE1011), LocalizableStrings.MIGRATE1011, message);

        public static Func<string, MigrationError> MIGRATE1012
            => (message) => new MigrationError(nameof(MIGRATE1012), LocalizableStrings.MIGRATE1012, message);

        public static Func<string, MigrationError> MIGRATE1013
            => (message) => new MigrationError(nameof(MIGRATE1013), LocalizableStrings.MIGRATE1013, message);

        public static Func<string, MigrationError> MIGRATE1014
            => (message) => new MigrationError(nameof(MIGRATE1014), LocalizableStrings.MIGRATE1014, message);

        public static Func<string, MigrationError> MIGRATE1015
            => (message) => new MigrationError(nameof(MIGRATE1015), LocalizableStrings.MIGRATE1015, message);

        public static Func<string, MigrationError> MIGRATE1016
            => (message) => new MigrationError(nameof(MIGRATE1016), LocalizableStrings.MIGRATE1016, message);

        public static Func<string, MigrationError> MIGRATE1017
            => (message) => new MigrationError(nameof(MIGRATE1017), LocalizableStrings.MIGRATE1017, message);

        public static Func<string, MigrationError> MIGRATE1018
            => (message) => new MigrationError(nameof(MIGRATE1018), LocalizableStrings.MIGRATE1018, message);

        public static Func<string, MigrationError> MIGRATE1019
            => (message) => new MigrationError(nameof(MIGRATE1019), LocalizableStrings.MIGRATE1019, message);

        // Potentially Temporary (Point in Time) Errors
        public static Func<string, MigrationError> MIGRATE20011
            => (message) => new MigrationError(nameof(MIGRATE20011), LocalizableStrings.MIGRATE20011, message);

        public static Func<string, MigrationError> MIGRATE20012
            => (message) => new MigrationError(nameof(MIGRATE20012), LocalizableStrings.MIGRATE20012, message);

        public static Func<string, MigrationError> MIGRATE20013
            => (message) => new MigrationError(nameof(MIGRATE20013), LocalizableStrings.MIGRATE20013, message);

        public static Func<string, MigrationError> MIGRATE20018
            => (message) => new MigrationError(nameof(MIGRATE20018), LocalizableStrings.MIGRATE20018, message);
    }
}