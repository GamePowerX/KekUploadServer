using System.Security.Cryptography;

namespace KekUploadServer;

public static class Utils
{
    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    public static async Task<string?> GetMimeType(string extension)
    {
        var mimeTypeEnumerable = await Task.Run(() => MimeTypeMap.List.MimeTypeMap.GetMimeType(extension));
        return mimeTypeEnumerable.FirstOrDefault();
    }
}