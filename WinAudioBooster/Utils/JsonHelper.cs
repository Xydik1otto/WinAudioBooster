using System.Text.Json;

namespace WinAudioBooster.Utils;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static T? ReadJson<T>(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return default;
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch
        {
            return default;
        }
    }

    public static void WriteJson<T>(string filePath, T data)
    {
        try
        {
            string json = JsonSerializer.Serialize(data, Options);
            File.WriteAllText(filePath, json);
        }
        catch { }
    }
}