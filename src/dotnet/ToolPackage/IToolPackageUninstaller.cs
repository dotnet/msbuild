using Microsoft.Extensions.EnvironmentAbstractions;

internal interface IToolPackageUninstaller
{
    void Uninstall(DirectoryPath packageDirectory);
}