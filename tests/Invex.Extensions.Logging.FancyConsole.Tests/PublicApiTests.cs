using Invex.Extensions.Logging.FancyConsole;

namespace Microsoft.Extensions.Logging;

[TestFixture]
public class PublicApiTests
{
    [Test]
    public async Task VerifyPublicApiSurface() =>
        await VerifyJson(PublicApiSurfaceTestUtil.GetPublicApiSurface(typeof(FancyConsoleLoggerExtensions).Assembly));
}
