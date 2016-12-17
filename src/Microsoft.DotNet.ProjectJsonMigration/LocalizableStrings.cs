// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class LocalizableStrings
    {
        public const string DoubleMigrationError = "Detected double project migration: {0}";

        public const string CannotMergeMetadataError = "Cannot merge metadata with the same name and different values";

        public const string NoXprojFileGivenError = "{0}: No xproj file given.";

        public const string MultipleXprojFilesError = "Multiple xproj files found in {0}, please specify which to use";

        public const string NullMSBuildProjectTemplateError = "Expected non-null MSBuildProjectTemplate in MigrationSettings";

        public const string CannotMigrateProjectWithCompilerError = "Cannot migrate project {0} using compiler {1}";

        public const string ExpectedElementToBeOfTypeNotTypeError = "Expected element to be of type {0}, but got {1}";

        public const string ProjAlreadyExistsError = "{0} already exists. Has migration already been run?";

        public const string NullDestinationElementError = "expected destinationElement to not be null";

        public const string DiagnosticMessageTemplate = "{0} (line: {1}, file: {2})";

        public const string CannotMergeItemsOfDifferentTypesError = "Cannot merge items of different types.";

        public const string CannotMergeItemsWithoutCommonIncludeError = "Cannot merge items without a common include.";

        public const string MIGRATE1011 = "Deprecated Project";

        public const string MIGRATE1012 = "Project not Restored";

        public const string MIGRATE1013 = "No Project";

        public const string MIGRATE1013Arg = "The project.json specifies no target frameworks in {0}";

        public const string MIGRATE1014 = "Unresolved Dependency";

        public const string MIGRATE1014Arg = "Unresolved project dependency ({0})";

        public const string MIGRATE1015 = "File Overwrite";

        public const string MIGRATE1016 = "Unsupported Script Variable";

        public const string MIGRATE1016Arg = "{0} is currently an unsupported script variable for project migration";

        public const string MIGRATE1017 = "Multiple Xproj Files";

        public const string MIGRATE1018 = "Dependency Project not found";

        public const string MIGRATE1018Arg = "Dependency project not found ({0})" ;

        public const string MIGRATE1019 = "Unsupported Script Event Hook";

        public const string MIGRATE1019Arg = "{0} is an unsupported script event hook for project migration";

        public const string MIGRATE20011 = "Multi-TFM";

        public const string MIGRATE20012 = "Configuration Exclude";

        public const string MIGRATE20013 = "Non-Csharp App";

        public const string MIGRATE20018 = "Files specified under PackOptions";

        public const string IncludesNotEquivalent = "{0}.{1} includes not equivalent.";

        public const string ExcludesNotEquivalent = "{0}.{1} excludes not equivalent.";

        public const string RemovesNotEquivalent = "{0}.{1} removes not equivalent.";

        public const string MetadataDoesntExist = "{0}.{1} metadata doesn't exist {{ {2} {3} }}";

        public const string MetadataHasAnotherValue = "{0}.{1} metadata has another value {{ {2} {3} {4} }}";

        public const string AddingMetadataToItem = "{0}: Adding metadata to {1} item: {{ {2}, {3}, {4} }}";

        public const string SkipMigrationAlreadyMigrated = "{0}: Skip migrating {1}, it is already migrated.";

        public const string ExecutingRule = "Executing rule: {0}";

        public const string NoConfigurationOrFrameworkFoundInProject = "{0}: No configuration or framework build options found in project";

        public const string MigratingCountTargetFrameworks = "Migrating {0} target frameworks";

        public const string MigratingFramework = "Migrating framework {0}";

        public const string ImportsTransformNullFor = "{0}: imports transform null for {1}";

        public const string MigratingCountXprojToCsprojReferences = "{0}: Migrating {1} xproj to csproj references";

        public const string ExecutingMigrationRule = "{0}: Executing migration rule {1}";

        public const string ItemTransformApplicatorHeader = "{0}: Item {{ ItemType: {1}, Condition: {2}, Include: {3}, Exclude: {4}, Update: {5} }}";

        public const string ItemTransformApplicatorItemGroup = "{0}: ItemGroup {{ Condition: {1} }}";

        public const string ItemTransformAppliatorItemCompletelyMerged = "{0}: Item completely merged";

        public const string ItemTransformApplicatorAddItemHeader = "{0}: AddItemToItemGroup {{ ItemType: {1}, Condition: {2}, Include: {3}, Exclude: {4}, Update: {5} }}";

        public const string ItemTransformApplicatorMergingItemWithExistingItems = "{0}: Merging Item with {1} existing items with a different condition chain.";

        public const string ItemTransformApplicatorEncompassedIncludes = "{0}: encompassed includes {1}";

        public const string ItemTransformApplicatorRemovingItem = "{0}: Removing Item {{ ItemType: {1}, Condition: {2}, Include: {3}, Exclude: {4} }}";

        public const string ItemTransformApplicatorIgnoringItem = "{0}: Ignoring Item {{ ItemType: {1}, Condition: {2}, Include: {3}, Exclude: {4} }}";

        public const string ItemTransformApplicatorMergingItemWithExistingItemsSameChain = "{0}: Merging Item with {1} existing items with the same condition chain.";

        public const string ItemTransformApplicatorAddingMergedItem = "{0}: Adding Merged Item {{ ItemType: {1}, Condition: {2}, Include: {3}, Exclude: {4} }}";
    }
}