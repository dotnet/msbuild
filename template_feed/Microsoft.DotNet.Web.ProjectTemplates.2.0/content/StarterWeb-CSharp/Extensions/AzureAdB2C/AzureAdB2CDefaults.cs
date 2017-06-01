// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    /// <summary>
    /// Default values for AzureAdB2C authentication
    /// </summary>
    public static class AzureAdB2CDefaults
    {
        public const string AuthenticationScheme = "AzureAdB2C";
        public const string BearerAuthenticationScheme = "AzureAdB2CBearer";
        public const string SignUpSignInAuthenticationScheme = "SignInSignUp";
        public const string ResetPasswordAuthenticationScheme = "ResetPassword";
        public const string EditProfileAuthenticationScheme = "EditProfile";
    }
}
