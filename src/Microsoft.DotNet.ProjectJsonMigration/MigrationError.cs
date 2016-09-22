// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigrationError
    {
        public string ErrorCode { get; }

        public string GeneralErrorReason { get; }

        public string Message { get; }

        public MigrationError(string errorCode, string generalErrorReason, string message)
        {
            ErrorCode = errorCode;
            GeneralErrorReason = generalErrorReason;
            Message = message;
        }

        public void Throw()
        {
            throw new MigrationException(GetFormattedErrorMessage());
        }

        public string GetFormattedErrorMessage()
        {
            return $"{ErrorCode}::{GeneralErrorReason}: {Message}";
        }
    }
}