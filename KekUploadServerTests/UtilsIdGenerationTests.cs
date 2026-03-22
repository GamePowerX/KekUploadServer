namespace KekUploadServerTests;

public class UtilsIdGenerationTests
{
    [Test]
    public void RandomString_HasExpectedLengthAndCharset()
    {
        var value = KekUploadServer.Utils.RandomString(64);

        Assert.That(value, Has.Length.EqualTo(64));
        Assert.That(value, Does.Match("^[a-z0-9]+$"));
    }

    [Test]
    public void RandomString_GeneratesDifferentValues()
    {
        var a = KekUploadServer.Utils.RandomString(32);
        var b = KekUploadServer.Utils.RandomString(32);

        Assert.That(a, Is.Not.EqualTo(b));
    }
}