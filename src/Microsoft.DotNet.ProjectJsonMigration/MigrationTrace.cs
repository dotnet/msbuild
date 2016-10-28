// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationTrace
    {
        public static MigrationTrace Instance { get; set; }

        static MigrationTrace ()
        {
            Instance = new MigrationTrace();
        }

        public string EnableEnvironmentVariable => "DOTNET_MIGRATION_TRACE";

        public bool IsEnabled
        {
            get
            {
#if DEBUG
                return true;
#else
                return Environment.GetEnvironmentVariable(EnableEnvironmentVariable) != null;
#endif
            }
        }

        public void WriteLine(string message)
        {
            if (IsEnabled)
            {
                Console.WriteLine(message);
            }
        }
    }
}