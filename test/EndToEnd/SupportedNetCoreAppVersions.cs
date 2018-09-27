using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EndToEnd
{
    public class SupportedNetCoreAppVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.Select(version => new object[] { version }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static IEnumerable<string> Versions
        {
            get
            {
                return new[]
                {
                    "1.0",
                    "1.1",
                    "2.0",
                    "2.1",
                    "2.2"
                };
            }
        }

        
    }

    public class SupportedAspNetCoreVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.Select(version => new object[] { version }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static IEnumerable<string> Versions
        {
            get
            {
                return SupportedNetCoreAppVersions.Versions.Except(new List<string>() { "1.0", "1.1", "2.0" });
            }
        }
    }
}
