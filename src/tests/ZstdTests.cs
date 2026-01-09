namespace zstd.net.tests;

public class ZstdTests
{
    [Fact]
    public void VersionString_ReturnsNonEmptyString()
    {
        var version = Zstd.VersionString();
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }
}
