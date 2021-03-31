using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.SharedLogic.Security
{
    public static class JsonConvertSecured
    {
        private const string VulnerabilityString = "$type";
        public static (T DeserializeObject, (string LogText, Tags unencryptedTags) LogInspectionItem) DeserializeObject<T>(string json, JsonSerializerSettings? settings)
        {
            if (settings?.TypeNameHandling == TypeNameHandling.None)
                return (JsonConvert.DeserializeObject<T>(json, settings), (null, null));

            var logItem = InspectJson(json);

            return (JsonConvert.DeserializeObject<T>(json, settings), logItem);
        }

        public static (Object? DeserializeObject, (string LogText, Tags unencryptedTags) LogInspectionItem) DeserializeObject(string json, Type returnType, JsonSerializerSettings settings)
        {
            if (settings?.TypeNameHandling == TypeNameHandling.None)
                return (JsonConvert.DeserializeObject(json, returnType, settings), (null, null));

            var logItem = InspectJson(json);

            return (JsonConvert.DeserializeObject(json, returnType, settings), logItem);
        }

        private static (string LogText, Tags unencryptedTags) InspectJson(string json)
        {
            (string LogText, Tags unencryptedTags) logItem = (null, null);
            var sp = Stopwatch.StartNew();
            List<string> valuesToInspect = null;
            try
            {
                var jObj = JObject.Parse(json);
                var nestedJObjects = new Queue<JObject>();
                nestedJObjects.Enqueue(jObj);

                while (nestedJObjects.Count > 0)
                {
                    foreach (KeyValuePair<string, JToken> jProperty in jObj)
                    {
                        if (jProperty.Key.Equals(VulnerabilityString,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            valuesToInspect ??= new List<string>();

                            try
                            {
                                valuesToInspect.Add(jProperty.Value?.ToString() ?? "null");
                            }
                            catch (Exception ex)
                            {
                                valuesToInspect.Add($"ExceptionAddValue - {ex.Message}");
                            }
                        }

                        if (jProperty.Value is JObject value)
                        {
                            nestedJObjects.Enqueue(value);
                        }
                    }

                    nestedJObjects.Dequeue();
                    if (nestedJObjects.Count > 0)
                        jObj = nestedJObjects.Peek();
                }
            }
            catch (Exception ex)
            {
                valuesToInspect ??= new List<string>();
                valuesToInspect.Add($"ExceptionParseJson - {ex.Message}");
            }

            try
            {
                if (valuesToInspect != null)
                {
                    var tags = new Tags();
                    int i = 0;
                    foreach (var val in valuesToInspect)
                        tags.Add($"ValueToInspect{i}", val ?? "null");

                    tags.Add("TotalInspectionTimeMs_l", sp.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

                    logItem = ("Json_Value_To_Inspect", unencryptedTags: tags);
                }
            }
            catch
            {
                // ignore
            }

            return logItem;
        }
    }

}
