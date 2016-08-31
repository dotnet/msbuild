using System;

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
            throw new Exception(GetFormattedErrorMessage());
        }

        public string GetFormattedErrorMessage()
        {
            return $"{ErrorCode}::{GeneralErrorReason}: {Message}";
        }
    }
}