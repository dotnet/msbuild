using System;

namespace Microsoft.DotNet.Tools.New3
{
    internal class LocalizableStrings
    {
        public const string DisplaysHelp = "Displays help for this command.";

        public const string ParameterNamePrefixError = "Parameter names must start with -- or -";

        public const string ParameterReuseError = "Parameter name {0} cannot be used for multiple purposes";

        public const string MultipleValuesSpecifiedForSingleValuedParameter = "Multiple values specified for single value parameter: {0}";

        public const string ValueSpecifiedForValuelessParameter = "Value specified for valueless parameter: {0}";

        public const string ParameterSpecifiedMultipleTimes = "Parameter [{0}] was specified multiple times, including with the flag [{1}]";

        public const string ParameterMissingValue = "Parameter [{0}] ({1}) must be given a value";

        public const string TemplateMalformedDueToBadParameters = "Template is malformed. The following parameter names are invalid: {0}";

        public const string OptionVariantAlreadyDefined = "Option variant {0} for canonical {1} was already defined for canonical {2}";

        public const string ListsTemplates = "List templates containing the specified name.";

        public const string NameOfOutput = "The name for the output being created. If no name is specified, the name of the current directory is used.";

        public const string CreateDirectoryHelp = "Indicates whether to create a directory for the generated content.";

        public const string CreateAliasHelp = "Creates an alias for the specified template.";

        public const string ExtraArgsFileHelp = "Specifies a file containing additional parameters.";

        public const string LocaleHelp = "The locale to use";

        public const string QuietModeHelp = "Doesn't output any status information.";

        public const string InstallHelp = "Installs a source or a template pack.";

        public const string UpdateHelp = "Update matching templates.";

        public const string CommandDescription = "Template Instantiation Commands for .NET Core CLI.";

        public const string TemplateArgumentHelp = "The template to instantiate.";

        public const string BadLocaleError = "Invalid format for input locale: [{0}]. Example valid formats: [en] [en-US]";

        public const string AliasCreated = "Alias creation successful";

        public const string AliasAlreadyExists = "Specified alias {0} already exists. Please specify a different alias.";

        public const string CreateSuccessful = "The template {0} created successfully.";

        public const string CreateFailed = "Template {0} could not be created. Error returned was: {1}";

        public const string InstallSuccessful = "{0} was installed successfully.";

        public const string InstallFailed = "{0} could not be installed. Error returned was: {1}.";

        public const string MissingRequiredParameter = "Mandatory parameter {0} missing for template {1}.";

        public const string GettingReady = "Getting ready...";

        public const string InvalidInputSwitch = "Invalid input switch:";

        public const string CheckingForUpdates = "Checking for updates for {0}...";

        public const string UpdateAvailable = "An update for {0} is available...";

        public const string NoUpdates = "No updates were found.";

        public const string InstallingUpdates = "Installing updates...";

        public const string BadPackageSpec = "Package [{0}] is not a valid package specification";

        public const string Templates = "Templates";

        public const string ShortName = "Short Name";

        public const string Alias = "Alias";

        public const string CurrentConfiguration = "Current configuration:";

        public const string NoItems = "(No Items)";

        public const string MountPoints = "Mount Points";

        public const string MountPointFactories = "Mount Point Factories";

        public const string Generators = "Generators";

        public const string Id = "Id";

        public const string Parent = "Parent";

        public const string Assembly = "Assembly";

        public const string Type = "Type";

        public const string Factory = "Factory";

        public const string Author = "Author: {0}";

        public const string Description = "Description: {0}";

        public const string Options = "Options:";

        public const string ConfiguredValue = "Configured Value: {0}";

        public const string DefaultValue = "Default: {0}";

        public const string NoParameters = "    (No Parameters)";
    }
}
