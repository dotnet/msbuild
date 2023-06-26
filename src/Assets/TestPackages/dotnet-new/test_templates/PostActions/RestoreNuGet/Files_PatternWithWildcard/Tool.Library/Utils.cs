using Newtonsoft.Json;

namespace Tool.Library
{
    public class Utils
    {
        public static object Deserialize(string value)
        {
            return JsonConvert.DeserializeObject(value);
        }

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }
}