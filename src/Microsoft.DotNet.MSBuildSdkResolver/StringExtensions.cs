namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static class StringExtensions
    {
        public static int FindFirstNotOf(this string s, string chars, int startIndex)
        {
            for (int i = startIndex; i < s.Length; i++)
            {
                if (chars.IndexOf(s[i]) == -1) return i;
            }

            return -1;
        }
    }
}