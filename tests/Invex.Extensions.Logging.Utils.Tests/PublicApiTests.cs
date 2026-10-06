namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Protects the complete public utilities API against unintended changes.
/// </summary>
[TestFixture]
public sealed class PublicApiTests
{
    /// <summary>
    ///     Verifies the public types and members against the committed snapshot.
    /// </summary>
    [Test]
    public async Task VerifyPublicApiSurface() =>
        await VerifyJson(PublicApiSurfaceTestUtil.GetPublicApiSurface(typeof(LogUtil).Assembly));
}
