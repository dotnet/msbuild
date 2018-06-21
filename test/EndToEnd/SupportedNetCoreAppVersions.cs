using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EndToEnd
{
    public class SupportedNetCoreAppVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static IEnumerable<object[]> Versions
        {
            get
            {
                return new[]
                {
                    "1.0",
                    "1.1",
                    "2.0",
                    "2.1"
                }.Select(version => new object[] { version });
            }
        }
    }
}
