using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1.Pages.Account.Manage
{
    public static class ManageMessages
    {
        public static string AddPhoneSuccess => "Your phone number was added.";

        public static string AddLoginSuccess => "The external login was added.";

        public static string ChangePasswordSuccess => "Your password has been changed.";

        public static string SetTwoFactorSuccess => "Your two-factor authentication provider has been set.";

        public static string SetPasswordSuccess => "Your password has been set.";

        public static string RemoveLoginSuccess => "The external login was removed.";

        public static string RemovePhoneSuccess => "Your phone number was removed.";

        public static string Error => "An error has occurred.";
    }
}
