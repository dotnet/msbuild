using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class AliasAssignmentTests
    {
        // also asserts that "--param:<name>" is used if <name> is taken
        [Fact(DisplayName = nameof(LongNameOverrideTakesPrecendence))]
        public void LongNameOverrideTakesPrecendence()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "foo",
                "bar",
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>()
            {
                { "bar", "foo" }    // bar explicitly wants foo for its long form
            };
            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>();

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal("--param:foo", assignmentCoordinator.LongNameAssignments["foo"]);
            Assert.Equal("-f", assignmentCoordinator.ShortNameAssignments["foo"]);
            Assert.Equal("--foo", assignmentCoordinator.LongNameAssignments["bar"]);
            Assert.Equal("-fo", assignmentCoordinator.ShortNameAssignments["bar"]); // the short name is based on the long name override if it exists
            Assert.Empty(assignmentCoordinator.InvalidParams);

        }

        [Fact(DisplayName = nameof(ShortNameOverrideTakesPrecedence))]
        public void ShortNameOverrideTakesPrecedence()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "foo",
                "bar",
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>();
            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>()
            {
                { "bar", "f" }  // bar explicitly wants f for its short form
            };

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal("--foo", assignmentCoordinator.LongNameAssignments["foo"]);
            Assert.Equal("-fo", assignmentCoordinator.ShortNameAssignments["foo"]);
            Assert.Equal("--bar", assignmentCoordinator.LongNameAssignments["bar"]);
            Assert.Equal("-f", assignmentCoordinator.ShortNameAssignments["bar"]);
            Assert.Empty(assignmentCoordinator.InvalidParams);
        }

        [Fact(DisplayName = nameof(ShortNameExcludedWithEmptyStringOverride))]
        public void ShortNameExcludedWithEmptyStringOverride()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "foo",
                "bar",
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>();
            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>()
            {
                { "bar", "" }  // bar explicitly wants f for its short form
            };

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal("--foo", assignmentCoordinator.LongNameAssignments["foo"]);
            Assert.Equal("-f", assignmentCoordinator.ShortNameAssignments["foo"]);
            Assert.Equal("--bar", assignmentCoordinator.LongNameAssignments["bar"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("bar", out string placeholder));
            Assert.Empty(assignmentCoordinator.InvalidParams);
        }

        [Fact(DisplayName = nameof(ParameterNameCannotContainColon))]
        public void ParameterNameCannotContainColon()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "foo:bar",
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>();
            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>();

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal(0, assignmentCoordinator.LongNameAssignments.Count);
            Assert.Equal(0, assignmentCoordinator.ShortNameAssignments.Count);
            Assert.Single(assignmentCoordinator.InvalidParams);
            Assert.Contains("foo:bar", assignmentCoordinator.InvalidParams);
        }

        [Fact(DisplayName = nameof(ShortNameGetPrependedPColonIfNeeded))]
        public void ShortNameGetPrependedPColonIfNeeded()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "bar",
                "f"
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>();
            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>()
            {
                { "bar", "f" }
            };

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal("--bar", assignmentCoordinator.LongNameAssignments["bar"]);
            Assert.Equal("-f", assignmentCoordinator.ShortNameAssignments["bar"]);
            Assert.Equal("--f", assignmentCoordinator.LongNameAssignments["f"]);
            Assert.Equal("-p:f", assignmentCoordinator.ShortNameAssignments["f"]);
        }

        // This reflects the MVC 2.0 tempalte as of May 24, 2017
        [Fact(DisplayName = nameof(CheckAliasAssignmentsMvc20))]
        public void CheckAliasAssignmentsMvc20()
        {
            IReadOnlyList<string> paramNameList = new List<string>()
            {
                "auth",
                "AAdB2CInstance",
                "SignUpSignInPolicyId",
                "ResetPasswordPolicyId",
                "EditProfilePolicyId",
                "AADInstance",
                "ClientId",
                "Domain",
                "TenantId",
                "CallbackPath",
                "OrgReadAccess",
                "UserSecretsId",
                "IncludeLaunchSettings",
                "HttpsPort",
                "KestrelPort",
                "IISExpressPort",
                "UseLocalDB",
                "TargetFrameworkOverride",
                "Framework",
                "NoTools",
                "skipRestore",
            };
            IReadOnlyList<ITemplateParameter> parameters = ParameterNamesToParametersTransform(paramNameList);

            IDictionary<string, string> longNameOverrides = new Dictionary<string, string>()
            {
                { "TargetFrameworkOverride", "target-framework-override" },
                { "UseLocalDB", "use-local-db" },
                { "AADInstance", "aad-instance" },
                { "AAdB2CInstance", "aad-b2c-instance" },
                { "SignUpSignInPolicyId", "susi-policy-id" },
                { "ResetPasswordPolicyId", "reset-password-policy-id" },
                { "EditProfilePolicyId", "edit-profile-policy-id" },
                { "OrgReadAccess", "org-read-access" },
                { "ClientId", "client-id" },
                { "CallbackPath", "callback-path" },
                { "Domain", "domain" },
                { "TenantId", "tenant-id" },
                { "Framework", "framework" },
                { "NoTools", "no-tools" },
                { "skipRestore", "no-restore" },
            };

            IDictionary<string, string> shortNameOverrides = new Dictionary<string, string>()
            {
                { "TargetFrameworkOverride", "" },
                { "AADInstance", "" },
                { "AAdB2CInstance", "" },
                { "SignUpSignInPolicyId", "ssp" },
                { "ResetPasswordPolicyId", "rp" },
                { "EditProfilePolicyId", "ep" },
                { "OrgReadAccess", "r" },
                { "ClientId", "" },
                { "CallbackPath", "" },
                { "Domain", "" },
                { "TenantId", "" },
                { "skipRestore", "" },
            };

            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameters, longNameOverrides, shortNameOverrides, InitiallyTakenAliases);

            Assert.Equal("-au", assignmentCoordinator.ShortNameAssignments["auth"]);
            Assert.Equal("--auth", assignmentCoordinator.LongNameAssignments["auth"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("AAdB2CInstance", out string placeholder));
            Assert.Equal("--aad-b2c-instance", assignmentCoordinator.LongNameAssignments["AAdB2CInstance"]);
            Assert.Equal("-ssp", assignmentCoordinator.ShortNameAssignments["SignUpSignInPolicyId"]);
            Assert.Equal("--susi-policy-id", assignmentCoordinator.LongNameAssignments["SignUpSignInPolicyId"]);
            Assert.Equal("-rp", assignmentCoordinator.ShortNameAssignments["ResetPasswordPolicyId"]);
            Assert.Equal("--reset-password-policy-id", assignmentCoordinator.LongNameAssignments["ResetPasswordPolicyId"]);
            Assert.Equal("-ep", assignmentCoordinator.ShortNameAssignments["EditProfilePolicyId"]);
            Assert.Equal("--edit-profile-policy-id", assignmentCoordinator.LongNameAssignments["EditProfilePolicyId"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("AADInstance", out placeholder));
            Assert.Equal("--aad-instance", assignmentCoordinator.LongNameAssignments["AADInstance"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("ClientId", out placeholder));
            Assert.Equal("--client-id", assignmentCoordinator.LongNameAssignments["ClientId"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("Domain", out placeholder));
            Assert.Equal("--domain", assignmentCoordinator.LongNameAssignments["Domain"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("TenantId", out placeholder));
            Assert.Equal("--tenant-id", assignmentCoordinator.LongNameAssignments["TenantId"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("CallbackPath", out placeholder));
            Assert.Equal("--callback-path", assignmentCoordinator.LongNameAssignments["CallbackPath"]);
            Assert.Equal("-r", assignmentCoordinator.ShortNameAssignments["OrgReadAccess"]);
            Assert.Equal("--org-read-access", assignmentCoordinator.LongNameAssignments["OrgReadAccess"]);
            Assert.Equal("-U", assignmentCoordinator.ShortNameAssignments["UserSecretsId"]);
            Assert.Equal("--UserSecretsId", assignmentCoordinator.LongNameAssignments["UserSecretsId"]);
            Assert.Equal("-I", assignmentCoordinator.ShortNameAssignments["IncludeLaunchSettings"]);
            Assert.Equal("--IncludeLaunchSettings", assignmentCoordinator.LongNameAssignments["IncludeLaunchSettings"]);
            Assert.Equal("-H", assignmentCoordinator.ShortNameAssignments["HttpsPort"]);
            Assert.Equal("--HttpsPort", assignmentCoordinator.LongNameAssignments["HttpsPort"]);
            Assert.Equal("-K", assignmentCoordinator.ShortNameAssignments["KestrelPort"]);
            Assert.Equal("--KestrelPort", assignmentCoordinator.LongNameAssignments["KestrelPort"]);
            Assert.Equal("-II", assignmentCoordinator.ShortNameAssignments["IISExpressPort"]);
            Assert.Equal("--IISExpressPort", assignmentCoordinator.LongNameAssignments["IISExpressPort"]);
            Assert.Equal("-uld", assignmentCoordinator.ShortNameAssignments["UseLocalDB"]);
            Assert.Equal("--use-local-db", assignmentCoordinator.LongNameAssignments["UseLocalDB"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("TargetFrameworkOverride", out placeholder));
            Assert.Equal("--target-framework-override", assignmentCoordinator.LongNameAssignments["TargetFrameworkOverride"]);
            Assert.Equal("-f", assignmentCoordinator.ShortNameAssignments["Framework"]);
            Assert.Equal("--framework", assignmentCoordinator.LongNameAssignments["Framework"]);
            Assert.Equal("-nt", assignmentCoordinator.ShortNameAssignments["NoTools"]);
            Assert.Equal("--no-tools", assignmentCoordinator.LongNameAssignments["NoTools"]);
            Assert.False(assignmentCoordinator.ShortNameAssignments.TryGetValue("SkipRestore", out placeholder));
            Assert.Equal("--no-restore", assignmentCoordinator.LongNameAssignments["skipRestore"]);
            Assert.Empty(assignmentCoordinator.InvalidParams);
        }

        // fills in enough of the parameter info for alias assignment
        private static IReadOnlyList<ITemplateParameter> ParameterNamesToParametersTransform(IReadOnlyList<string> paramNameList)
        {
            List<ITemplateParameter> parameterList = new List<ITemplateParameter>();

            foreach (string paramName in paramNameList)
            {
                ITemplateParameter parameter = new MockParameter()
                {
                    Name = paramName,
                    Priority = TemplateParameterPriority.Required,
                };

                parameterList.Add(parameter);
            }

            return parameterList;
        }

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
    }
}
