namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    internal class LocalizableStrings
    {
        /// del p2p
        public const string ReferenceNotFoundInTheProject = "Specified reference {0} does not exist in project {1}.";
        
        public const string ReferenceRemoved = "Reference `{0}` deleted from the project.";
        
        public const string SpecifyAtLeastOneReferenceToRemove = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";
        
        public const string ReferenceDeleted = "Reference `{0}` deleted.";
        
        public const string SpecifyAtLeastOneReferenceToDelete = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";
    }
}