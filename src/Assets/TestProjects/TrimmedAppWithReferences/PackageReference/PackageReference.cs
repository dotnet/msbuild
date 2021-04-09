using System.Diagnostics.CodeAnalysis;

namespace PackageReference
{
    public class PackageReferenceLib
    {
        public static void Method() {
            RUC();
        }

        [RequiresUnreferencedCode ("Testing IL2026 in PackageReference")]
        static void RUC() {
        }
    }
}
