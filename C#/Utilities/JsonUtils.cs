using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CXP.Utilities
{
    public partial class JsonUtils
    {
        public static string AppendJson(string json, string key, string value)
        {
            try
            {
                JsonNode? jsonDoc = JsonNode.Parse(json);
                if (jsonDoc != null)
                {
                    if (jsonDoc[key] == null)
                    {
                        jsonDoc[key] = value;
                    }
                    return jsonDoc.ToJsonString();
                }
            }
            catch
            {
            }
            return json;
        }
    }
}
