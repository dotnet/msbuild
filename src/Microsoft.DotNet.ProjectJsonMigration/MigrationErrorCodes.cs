// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public static partial class MigrationErrorCodes
    {
        public static Func<string, MigrationError> MIGRATE1011
            => (message) => new MigrationError(nameof(MIGRATE1011), "Deprecated Project", message);

        public static Func<string, MigrationError> MIGRATE1012
            => (message) => new MigrationError(nameof(MIGRATE1012), "Project not Restored", message);

        public static Func<string, MigrationError> MIGRATE1013
            => (message) => new MigrationError(nameof(MIGRATE1013), "No Project", message);

        public static Func<string, MigrationError> MIGRATE1014
            => (message) => new MigrationError(nameof(MIGRATE1014), "Unresolved Dependency", message);

        public static Func<string, MigrationError> MIGRATE1015
            => (message) => new MigrationError(nameof(MIGRATE1015), "File Overwrite", message);

        public static Func<string, MigrationError> MIGRATE1016
            => (message) => new MigrationError(nameof(MIGRATE1016), "Unsupported Script Variable", message);

        public static Func<string, MigrationError> MIGRATE1017
            => (message) => new MigrationError(nameof(MIGRATE1017), "Multiple Xproj Files", message);

        // Potentially Temporary (Point in Time) Errors
        public static Func<string, MigrationError> MIGRATE20011
            => (message) => new MigrationError(nameof(MIGRATE20011), "Multi-TFM", message);

        public static Func<string, MigrationError> MIGRATE20012
            => (message) => new MigrationError(nameof(MIGRATE20012), "Configuration Exclude", message);

        public static Func<string, MigrationError> MIGRATE20013
            => (message) => new MigrationError(nameof(MIGRATE20013), "Non-Csharp App", message);
    }
}