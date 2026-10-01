namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class PublicApiTests
{
    [Test]
    public async Task VerifyPublicApiSurface() =>
        await VerifyJson(PublicApiSurfaceTestUtil.GetPublicApiSurface(typeof(FancyConsoleLoggerExtensions).Assembly));
}
