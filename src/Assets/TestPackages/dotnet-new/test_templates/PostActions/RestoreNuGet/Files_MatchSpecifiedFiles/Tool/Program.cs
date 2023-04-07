using Newtonsoft.Json;
using Tool.Library;

namespace Tool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            string filed = "Schedule";
            string frequence = "Daily";
            string value = $"{{\"{filed}\": \"{frequence}\"}}";
            var schedule = Utils.Deserialize(value);
            var result = JsonConvert.SerializeObject(schedule);
        }
    }
}