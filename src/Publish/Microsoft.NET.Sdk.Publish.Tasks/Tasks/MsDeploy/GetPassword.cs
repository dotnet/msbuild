using System;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
                Byte[] uncrypted = System.Security.Cryptography.ProtectedData.Unprotect(encryptedData, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                plainPWD = Encoding.Unicode.GetString(uncrypted);
            }
            catch
            {
            }
            return plainPWD;
        }

    }
}
