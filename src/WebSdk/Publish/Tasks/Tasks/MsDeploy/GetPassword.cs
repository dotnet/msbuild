// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    public class GetPassword : Task
    {
        public string EncryptedPassword { get; set; }

        [Output]
        public string ClearPassword { get; set; }

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(EncryptedPassword))
            {
                ClearPassword = GetClearTextPassword(EncryptedPassword);
            }

            return true;
        }

        public string GetClearTextPassword(string base64EncodedString)
        {
            if (base64EncodedString == null)
            {
                return null;
            }

            var encryptedData = Convert.FromBase64String(base64EncodedString);
            return GetClearTextPassword(encryptedData);
        }

        public static string GetClearTextPassword(Byte[] encryptedData)
        {
            if (encryptedData == null)
            {
                return "";
            }

            string plainPWD = "";
            try
            {
#pragma warning disable CA1416 // This functionality is only expected to work in Windows.
                Byte[] uncrypted = System.Security.Cryptography.ProtectedData.Unprotect(encryptedData, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
#pragma warning restore CA1416 // This functionality is only expected to work in Windows.
                plainPWD = Encoding.Unicode.GetString(uncrypted);
            }
            catch
            {
            }
            return plainPWD;
        }

    }
}
