// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationTrace : TextWriter
    {
        public static MigrationTrace Instance { get; set; }

        public string EnableEnvironmentVariable => "DOTNET_MIGRATION_TRACE";
        public bool IsEnabled { get; private set; }

        private TextWriter _underlyingWriter;

        static MigrationTrace ()
        {
            Instance = new MigrationTrace();
        }

        public MigrationTrace()
        {
            _underlyingWriter = Console.Out;
            IsEnabled = IsEnabledValue();
        }

        private bool IsEnabledValue()
        {
#if DEBUG
            return true;
#else
            return Environment.GetEnvironmentVariable(EnableEnvironmentVariable) != null;
#endif
        }

        public override Encoding Encoding => _underlyingWriter.Encoding;

        public override void Write(char value)
        {
            if (IsEnabled)
            {
                _underlyingWriter.Write(value);
            }
        }
    }
}