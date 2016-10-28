// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationException : Exception 
    { 
        public MigrationError Error { get; }
        public MigrationException(MigrationError error, string message) : base(message) 
        { 
            Error = error;
        }
    }
}