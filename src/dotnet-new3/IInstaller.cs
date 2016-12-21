using System.Collections.Generic;

namespace dotnet_new3
{
    public interface IInstaller
    {
        void InstallPackages(IEnumerable<string> installationRequests);
    }
}
