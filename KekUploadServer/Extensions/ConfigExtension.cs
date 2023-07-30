using Newtonsoft.Json.Linq;

namespace KekUploadServer.Extensions;

public static class ConfigExtension
{
    public static void SetValue<T>(this IConfiguration config, string key, T value, string configPath = "appsettings.json")
    {
        if(value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        config.GetSection(key).Value = value.ToString();
        // now make this change permanent
        configPath = Path.GetFullPath(configPath);
        var json = File.ReadAllText(configPath);
        var jObject = JObject.Parse(json);
        // : is the delimiter for nested keys
        key = key.Replace(":", ".");
        var jToken = jObject.SelectToken(key, false);
        if (jToken != null)
        {
            jToken.Replace(JToken.FromObject(value));
        }
        else
        {
            jObject.Add(key, JToken.FromObject(value));
        }
        json = jObject.ToString();
        File.WriteAllText(configPath, json);
    }
}