using System.Diagnostics.CodeAnalysis;
using ProjectReference;
using PackageReference;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            RUC();
            UseProjectReference();
            UseTransitiveProjectReference();
            UsePackageReference();
        }

        [RequiresUnreferencedCode ("Testing IL2026 in IntermediateAssembly")]
        static void RUC() {
        }

        static void UseProjectReference() {
            ProjectReferenceLib.Method();
        }

        static void UseTransitiveProjectReference() {
            ProjectReferenceLib.UseTransitiveProjectReference();
        }

        static void UsePackageReference() {
            PackageReferenceLib.Method();
        }
    }
}
