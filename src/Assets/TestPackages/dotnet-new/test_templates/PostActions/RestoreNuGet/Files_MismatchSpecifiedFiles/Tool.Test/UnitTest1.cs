using Newtonsoft.Json;
using Tool.Library;

namespace Tool.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            string filed = "Schedule";
            string frequence = "Daily";
            string value = $"{{\"{filed}\": \"{frequence}\"}}";
            var schedule = Utils.Deserialize(value);
            var result = JsonConvert.SerializeObject(schedule);
            Assert.Contains(filed, result);
            Assert.Contains(frequence, result);
        }
    }
}