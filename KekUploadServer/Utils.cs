namespace KekUploadServer;

public class Utils
{
    public static string RandomString(int i)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, i)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}