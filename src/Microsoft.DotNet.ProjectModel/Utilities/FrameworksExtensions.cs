using System.Linq;

namespace NuGet.Frameworks
{
    public static class FrameworksExtensions
    {
        // HACK(anurse): NuGet.Frameworks turns "dnxcore50" into "dnxcore5" :(
        public static string GetTwoDigitShortFolderName(this NuGetFramework self)
        {
            var original = self.GetShortFolderName();

            var digits = original.SkipWhile(c => !char.IsDigit(c)).ToArray();
            if(digits.Length == 1)
            {
                return original + "0";
            }
            return original;
        }
    }
}
