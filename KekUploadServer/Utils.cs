namespace KekUploadServer;

public static class Utils
{
    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static async Task<string?> GetMimeType(string extension)
    {
        var mimeTypeEnumerable = await Task.Run(() => MimeTypeMap.List.MimeTypeMap.GetMimeType(extension));
        return mimeTypeEnumerable.FirstOrDefault();
    }
}