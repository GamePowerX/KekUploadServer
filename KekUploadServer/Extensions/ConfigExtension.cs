using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KekUploadServer.Extensions;

public static class ConfigExtension
{
    public static void SetValue<T>(this IConfiguration config, string key, T value,
        string configPath = "appsettings.json")
    {
        // Create a JSON object from the configuration file content
        JObject? jsonConfig;
        using (var streamReader = new StreamReader(configPath))
        using (var jsonReader = new JsonTextReader(streamReader))
        {
            jsonConfig = JObject.Load(jsonReader);
        }

        if (jsonConfig == null) throw new Exception("Failed to parse json!");

        // Split the key by ':' to get the nested path
        var keys = key.Split(':');

        // Traverse the nested path and create missing objects if necessary
        var current = jsonConfig;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            if (current![keys[i]] == null || current[keys[i]]!.Type != JTokenType.Object)
                current[keys[i]] = new JObject();
            current = (JObject)current[keys[i]]!;
        }

        // Remove the existing key if it exists
        if (current[keys[^1]] != null) current.Remove(keys[^1]);

        // Set the final value for the last key
        current[keys[^1]] = JToken.FromObject(value!);

        // Save the modified JSON back to the configuration file
        File.WriteAllText(configPath, jsonConfig.ToString(Formatting.Indented));
    }
}