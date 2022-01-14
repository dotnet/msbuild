using System.Diagnostics.CodeAnalysis;
using TransitiveProjectReference;

namespace ProjectReference
{
    public class ProjectReferenceLib
    {
        public static void Method() {
            RUC();
        }

        [RequiresUnreferencedCode ("Testing IL2026 in ProjectReference")]
        static void RUC() {
        }

        public static void UseTransitiveProjectReference() {
            TransitiveProjectReferenceLib.Method();
        }
    }
}
