// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Cli.Commands;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class AliasAssignmentTests
    {
        private static HashSet<string> InitiallyTakenAliases
        {
            get
            {
                HashSet<string> initiallyTakenAliases = new HashSet<string>()
                {
                    "-h", "--help",
                    "-l", "--list",
                    "-n", "--name",
                    "-o", "--output",
                    "-i", "--install",
                    "-u", "--uninstall",
                    "--type",
                    "--force",
                    "-lang", "--language",
                    "-a", "--alias",
                    "--show-alias",
                    "-x", "--extra-args",
                    "--quiet",
                    "-all", "--show-all",
                    "--allow-scripts",
                    "--baseline",
                    "-up", "--update",
                    "--skip-update-check"
                };

                return initiallyTakenAliases;
            }
        }

        // also asserts that "--param:<name>" is used if <name> is taken
        [Fact(DisplayName = nameof(LongNameOverrideTakesPrecendence))]
        public void LongNameOverrideTakesPrecendence()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("foo"),
                new CliTemplateParameter("bar", longNameOverrides: new[] { "foo" })
            };

            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);

            Assert.Contains("--param:foo", result["foo"].Aliases);
            Assert.Contains("-f", result["foo"].Aliases);
            Assert.Contains("--foo", result["bar"].Aliases);
            Assert.Contains("-fo", result["bar"].Aliases); // the short name is based on the long name override if it exists
            Assert.DoesNotContain(result, r => r.Value.Errors.Any());
        }

        [Fact(DisplayName = nameof(ShortNameOverrideTakesPrecedence))]
        public void ShortNameOverrideTakesPrecedence()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("foo"),
                new CliTemplateParameter("bar", shortNameOverrides: new[] { "f" })
            };

            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);

            Assert.Contains("--foo", result["foo"].Aliases);
            Assert.Contains("-fo", result["foo"].Aliases);
            Assert.Contains("--bar", result["bar"].Aliases);
            Assert.Contains("-f", result["bar"].Aliases);
            Assert.DoesNotContain(result, r => r.Value.Errors.Any());
        }

        [Fact(DisplayName = nameof(ShortNameExcludedWithEmptyStringOverride))]
        public void ShortNameExcludedWithEmptyStringOverride()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("foo"),
                new CliTemplateParameter("bar", shortNameOverrides: new[] { "" })
            };
  
            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);

            Assert.Contains("--foo", result["foo"].Aliases);
            Assert.Contains("-f", result["foo"].Aliases);
            Assert.Contains("--bar", result["bar"].Aliases);
            Assert.Single(result["bar"].Aliases);
            Assert.DoesNotContain(result, r => r.Value.Errors.Any());
        }

        [Fact(DisplayName = nameof(ParameterNameCannotContainColon))]
        public void ParameterNameCannotContainColon()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("foo:bar"),
            };

            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);
            Assert.Empty(result["foo:bar"].Aliases);
            Assert.Single(result["foo:bar"].Errors);
            Assert.Contains("Parameter name 'foo:bar' contains colon, which is forbidden.", result["foo:bar"].Errors);
        }

        [Fact(DisplayName = nameof(ShortNameGetPrependedPColonIfNeeded))]
        public void ShortNameGetPrependedPColonIfNeeded()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("bar", shortNameOverrides: new [] { "f" }),
                new CliTemplateParameter("f")
            };

            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);

            Assert.Contains("--bar", result["bar"].Aliases);
            Assert.Contains("-f", result["bar"].Aliases);
            Assert.Contains("--f", result["f"].Aliases);
            Assert.Contains("-p:f", result["f"].Aliases);
            Assert.DoesNotContain(result, r => r.Value.Errors.Any());
        }

        // This reflects the MVC 2.0 tempalte as of May 24, 2017
        [Fact(DisplayName = nameof(CheckAliasAssignmentsMvc20))]
        public void CheckAliasAssignmentsMvc20()
        {
            IReadOnlyList<CliTemplateParameter> paramList = new List<CliTemplateParameter>()
            {
                new CliTemplateParameter("auth"),
                new CliTemplateParameter("AAdB2CInstance", longNameOverrides: new [] { "aad-b2c-instance" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("SignUpSignInPolicyId", longNameOverrides: new [] { "susi-policy-id" }, shortNameOverrides: new [] { "ssp" }),
                new CliTemplateParameter("ResetPasswordPolicyId", longNameOverrides: new [] { "reset-password-policy-id" }, shortNameOverrides: new [] { "rp" }),
                new CliTemplateParameter("EditProfilePolicyId", longNameOverrides: new [] { "edit-profile-policy-id" }, shortNameOverrides: new [] { "ep" }),
                new CliTemplateParameter("AADInstance", longNameOverrides: new [] { "aad-instance" }, shortNameOverrides: new [] { "" } ),
                new CliTemplateParameter("ClientId", longNameOverrides: new [] { "client-id" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("Domain", longNameOverrides: new [] { "domain" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("TenantId", longNameOverrides: new [] { "tenant-id" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("CallbackPath", longNameOverrides: new [] { "callback-path" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("OrgReadAccess", longNameOverrides: new [] { "org-read-access" }, shortNameOverrides: new [] { "r" }),
                new CliTemplateParameter("UserSecretsId"),
                new CliTemplateParameter("IncludeLaunchSettings"),
                new CliTemplateParameter("HttpsPort"),
                new CliTemplateParameter("KestrelPort"),
                new CliTemplateParameter("IISExpressPort"),
                new CliTemplateParameter("UseLocalDB", longNameOverrides: new [] { "use-local-db" }),
                new CliTemplateParameter("TargetFrameworkOverride", longNameOverrides: new [] { "target-framework-override" }, shortNameOverrides: new [] { "" }),
                new CliTemplateParameter("Framework", longNameOverrides: new [] { "framework" }),
                new CliTemplateParameter("NoTools", longNameOverrides: new [] { "no-tools" }),
                new CliTemplateParameter("skipRestore", longNameOverrides: new [] { "no-restore" }, shortNameOverrides: new [] { "" })
            };

            var result = AliasAssignmentCoordinator.AssignAliasesForParameter(paramList, InitiallyTakenAliases).ToDictionary(r => r.Parameter.Name, r => r);

            Assert.Contains("-au", result["auth"].Aliases);
            Assert.Contains("--auth", result["auth"].Aliases);
            Assert.Single(result["AAdB2CInstance"].Aliases);
            Assert.Contains("--aad-b2c-instance", result["AAdB2CInstance"].Aliases);
            Assert.Contains("-ssp", result["SignUpSignInPolicyId"].Aliases);
            Assert.Contains("--susi-policy-id", result["SignUpSignInPolicyId"].Aliases);
            Assert.Contains("-rp", result["ResetPasswordPolicyId"].Aliases);
            Assert.Contains("--reset-password-policy-id", result["ResetPasswordPolicyId"].Aliases);
            Assert.Contains("-ep", result["EditProfilePolicyId"].Aliases);
            Assert.Contains("--edit-profile-policy-id", result["EditProfilePolicyId"].Aliases);
            Assert.Single(result["AADInstance"].Aliases);
            Assert.Contains("--aad-instance", result["AADInstance"].Aliases);
            Assert.Single(result["ClientId"].Aliases);
            Assert.Contains("--client-id", result["ClientId"].Aliases);
            Assert.Single(result["Domain"].Aliases);
            Assert.Contains("--domain", result["Domain"].Aliases);
            Assert.Single(result["TenantId"].Aliases);
            Assert.Contains("--tenant-id", result["TenantId"].Aliases);
            Assert.Single(result["CallbackPath"].Aliases);
            Assert.Contains("--callback-path", result["CallbackPath"].Aliases);
            Assert.Contains("-r", result["OrgReadAccess"].Aliases);
            Assert.Contains("--org-read-access", result["OrgReadAccess"].Aliases);
            Assert.Contains("-U", result["UserSecretsId"].Aliases);
            Assert.Contains("--UserSecretsId", result["UserSecretsId"].Aliases);
            Assert.Contains("-I", result["IncludeLaunchSettings"].Aliases);
            Assert.Contains("--IncludeLaunchSettings", result["IncludeLaunchSettings"].Aliases);
            Assert.Contains("-H", result["HttpsPort"].Aliases);
            Assert.Contains("--HttpsPort", result["HttpsPort"].Aliases);
            Assert.Contains("-K", result["KestrelPort"].Aliases);
            Assert.Contains("--KestrelPort", result["KestrelPort"].Aliases);
            Assert.Contains("-II", result["IISExpressPort"].Aliases);
            Assert.Contains("--IISExpressPort", result["IISExpressPort"].Aliases);
            Assert.Contains("-uld", result["UseLocalDB"].Aliases);
            Assert.Contains("--use-local-db", result["UseLocalDB"].Aliases);
            Assert.Single(result["TargetFrameworkOverride"].Aliases);
            Assert.Contains("--target-framework-override", result["TargetFrameworkOverride"].Aliases);
            Assert.Contains("-f", result["Framework"].Aliases);
            Assert.Contains("--framework", result["Framework"].Aliases);
            Assert.Contains("-nt", result["NoTools"].Aliases);
            Assert.Contains("--no-tools", result["NoTools"].Aliases);
            Assert.Single(result["skipRestore"].Aliases);
            Assert.Contains("--no-restore", result["skipRestore"].Aliases);
            Assert.DoesNotContain(result, r => r.Value.Errors.Any());
        }
    }
}
