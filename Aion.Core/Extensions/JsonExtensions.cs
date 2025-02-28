using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aion.Core.Extensions;

public static class JsonExtensions
{
    public static string ParseNestedJson(this string compressed)
    {
        try
        {
            return ParseNestedJson(JsonSerializer.Deserialize<Dictionary<string, string>>(compressed));
        }
        catch
        {
            return compressed;
        }
    }

    public static string ParseNestedJson(Dictionary<string, string> dict)
    {
        var jsonString = JsonSerializer.Serialize(dict);
        var jsonNode = JsonNode.Parse(jsonString);

        var tempDict = new Dictionary<string, JsonNode>();

        foreach (var property in jsonNode!.AsObject())
        {
            if (property.Value is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String)
            {
                var valueString = jsonValue.ToString();
                if ((valueString.StartsWith("{") && valueString.EndsWith("}")) || 
                    (valueString.StartsWith("[") && valueString.EndsWith("]")))
                {
                    try
                    {
                        var parsedNode = JsonNode.Parse(valueString);
                        tempDict[property.Key] = parsedNode!;
                    }
                    catch (JsonException)
                    {
                        tempDict[property.Key] = jsonValue;
                    }
                }
                else
                {
                    tempDict[property.Key] = jsonValue;
                }
            }
            else
            {
                tempDict[property.Key] = property.Value!;
            }
        }

        jsonNode.AsObject().Clear();
        foreach (var kvp in tempDict)
        {
            jsonNode.AsObject().Add(kvp.Key, kvp.Value);
        }

        return jsonNode.ToJsonString();
    }
}