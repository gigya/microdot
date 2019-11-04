using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.SharedLogic
{
    public static class DotNetCoreCompatibility
    {
        public static bool ShouldAdjustJsonToDotNetFramwork()
        {
            var envVar = Environment.GetEnvironmentVariable("GIGYA_ENABLE_JSON_COMPATIBILITY_SHIM");
            if (envVar == "true")
                return true;
            return false;
        }

        public static string AdjustJsonToDotNetFramework(string data)
        {
            if (string.IsNullOrEmpty(data)) return data;
            try
            {
                var json = JToken.Parse(data);
                var allTypeTokens = new List<JProperty>();
                PoplulateTokens(json, "$type", allTypeTokens);
                foreach (var item in allTypeTokens)
                {
                    if (item.Value.Type == JTokenType.String)
                        item.Value.Replace(item.Value.ToString().Replace("System.Private.CoreLib", "mscorlib"));
                }
                var ret = json.ToString(Formatting.Indented);
                return ret;
            }
            catch 
            {
                return data;
            }
        }

        private static void PoplulateTokens(JToken json, string tokenName, List<JProperty> allTokens)
        {
            if (json.Type == JTokenType.Object)
            {
                foreach (var prop in (json as JObject).Properties())
                {
                    if (prop.Name == tokenName)
                        allTokens.Add(prop);
                    else if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array)
                        PoplulateTokens(prop.Value, tokenName, allTokens);
                }
            }
            else if (json.Type == JTokenType.Array)
            {
                foreach (var item in (json as JArray))
                {
                    if (item.Type == JTokenType.Object || item.Type == JTokenType.Array)
                        PoplulateTokens(item, tokenName, allTokens);
                }
            }
        }
    }
}
