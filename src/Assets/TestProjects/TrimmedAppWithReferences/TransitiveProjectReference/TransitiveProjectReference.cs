using System.Diagnostics.CodeAnalysis;

namespace TransitiveProjectReference
{
    public class TransitiveProjectReferenceLib
    {
        public static void Method() {
            RUC();
        }

        [RequiresUnreferencedCode ("Testing IL2026 in TransitiveProjectReference")]
        static void RUC() {
        }
    }
}
