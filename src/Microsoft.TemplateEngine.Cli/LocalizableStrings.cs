// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TemplateEngine.Cli
{
    internal class LocalizableStrings
    {
        public const string ShowsAllTemplates = "Shows all templates.";

        public const string DisplaysHelp = "Displays help for this command.";

        public const string ParameterNamePrefixError = "Parameter names must start with -- or -.";

        public const string MultipleArgsSpecifiedError = "Template name is the only allowed argument. Invalid argument: [{0}].";

        public const string ParameterReuseError = "Parameter name {0} cannot be used for multiple purposes.";

        public const string MultipleValuesSpecifiedForSingleValuedParameter = "Multiple values specified for single value parameter: {0}.";

        public const string ValueSpecifiedForValuelessParameter = "Value specified for valueless parameter: {0}.";

        public const string ParameterSpecifiedMultipleTimes = "Parameter [{0}] was specified multiple times, including with the flag [{1}].";

        public const string ParameterMissingValue = "Parameter [{0}] ({1}) must be given a value.";

        public const string TemplateMalformedDueToBadParameters = "Template is malformed. The following parameter names are invalid: {0}.";

        public const string OptionVariantAlreadyDefined = "Option variant {0} for canonical {1} was already defined for canonical {2}";

        public const string ListsTemplates = "Lists templates containing the specified name. If no name is specified, lists all templates.";

        public const string NameOfOutput = "The name for the output being created. If no name is specified, the name of the current directory is used.";

        public const string OutputPath = "Location to place the generated output.";

        public const string CreateDirectoryHelp = "Indicates whether to create a directory for the generated content.";

        public const string CreateAliasHelp = "Creates an alias for the specified template.";

        public const string ExtraArgsFileHelp = "Specifies a file containing additional parameters.";

        public const string LocaleHelp = "The locale to use.";

        public const string QuietModeHelp = "Doesn't output any status information.";

        public const string InstallHelp = "Installs a source or a template pack.";

        public const string UpdateHelp = "Update matching templates.";

        public const string CommandDescription = "Template Instantiation Commands for .NET Core CLI";

        public const string TemplateArgumentHelp = "The template to instantiate.";

        public const string BadLocaleError = "Invalid format for input locale: \"{0}\". Example valid formats: [en] [en-US].";

        public const string AliasCreated = "Alias creation successful.";

        public const string AliasAlreadyExists = "Specified alias \"{0}\" already exists. Please specify a different alias.";

        public const string CreateSuccessful = "The template \"{0}\" was created successfully.";

        public const string CreateFailed = "Template \"{0}\" could not be created.\n{1}";

        public const string InstallSuccessful = "\"{0}\" was installed successfully.";

        public const string InstallFailed = "\"{0}\" could not be installed.\n{1}.";

        public const string MissingRequiredParameter = "Mandatory parameter {0} missing for template {1}.";

        public const string InvalidParameterValues = "Error: Invalid values for parameter(s) [{0}] for template [{1}].";

        public const string GettingReady = "Getting ready...";

        public const string InvalidInputSwitch = "Invalid input switch:";

        public const string ArgsFileNotFound = "The specified extra args file does not exist: [{0}].";

        public const string ArgsFileWrongFormat = "Extra args file [{0}] is not formatted properly.";

        public const string CheckingForUpdates = "Checking for updates for {0}...";

        public const string UpdateAvailable = "An update for {0} is available...";

        public const string NoUpdates = "No updates were found.";

        public const string InstallingUpdates = "Installing updates...";

        public const string BadPackageSpec = "Package \"{0}\" is not a valid package specification";

        public const string Templates = "Templates";

        public const string ShortName = "Short Name";

        public const string Alias = "Alias";

        public const string Tags = "Tags";

        public const string Language = "Language";

        public const string LanguageParameter = "Specifies the language of the template to create.";

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

        public const string AmbiguousInputTemplateName = "Unable to determine the desired template from the input template name: [{0}].";

        public const string NoTemplatesMatchName = "No templates matched the input template name: [{0}].";

        public const string ItemTemplateNotInProjectContext = "[{0}] is an item template. By default, it's only created in a target location containing a project. Force creation with the -all flag.";

        public const string ProjectTemplateInProjectContext = "[{0}] is a project template. By default, it's not created in a target location containing a project. Force creation with the -all flag.";

        public const string GenericPlaceholderTemplateContextError = "[{0}] cannot be created in the target location.";

        public const string TemplateMultiplePartialNameMatches = "The following templates partially match the input. Be more specific with the template name and/or language.";

        public const string DestructiveChangesNotification = "Creating this template will make changes to existing files:";

        public const string ContinueQuestion = "Continue?";

        public const string Change = "Change";

        public const string Delete = "Delete";

        public const string Overwrite = "Overwrite";

        public const string UnknownChangeKind = "Unknown Change";

        public const string WillCreateTemplate = "About to create: ";

        public const string RerunCommandAndPassForceToCreateAnyway = "Rerun the command and pass --force to accept and create.";

        public const string ForcesTemplateCreation = "Forces content to be generated even if it would change existing files.";
    }
}
