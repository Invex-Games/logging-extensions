namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class ScopeAndCategoryTests : TestBase
{
    [TestCase(FancyConsoleLayout.Standard)]
    [TestCase(FancyConsoleLayout.SingleLine)]
    [TestCase(FancyConsoleLayout.Minimal)]
    public void Scopes_Are_Excluded_By_Default(FancyConsoleLayout layout)
    {
        var logger = CreateLogger(c => c.Layout = layout);

        using (logger.BeginScope("Request 1"))
            logger.LogInformation("Hello");

        Output.ShouldNotContain("Request 1");
    }

    [Test]
    public void IncludeScopes_Writes_Scopes_After_The_Category_In_Standard()
    {
        var logger = CreateLogger(c => c.IncludeScopes = true);

        using (logger.BeginScope("Request {RequestId}", 7))
        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["User"] = "alice",
                   ["Tenant"] = 3,
               }))
            logger.LogInformation("Hello");

        Output.ShouldBe(Lf("""
                           2020-01-01 +11:00 MyApp.Services.OrderService => Request 7 => User=alice, Tenant=3
                           11:00:00.000  INF Hello


                           """));
    }

    [Test]
    public void IncludeScopes_Writes_Scopes_After_The_Category_In_SingleLine()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.SingleLine;
            c.IncludeScopes = true;
        });

        using (logger.BeginScope("Outer"))
        using (logger.BeginScope(new object()))
        using (logger.BeginScope(""))
        using (logger.BeginScope("Inner"))
            logger.LogInformation("Hello");

        Output.ShouldBe("11:00:00.000 INF MyApp.Services.OrderService => Outer => System.Object => Inner: Hello\n");
    }

    [Test]
    public void Scopes_Are_Removed_When_Disposed()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.SingleLine;
            c.IncludeScopes = true;
        });

        using (logger.BeginScope("Scoped"))
            logger.LogInformation("Inside");

        logger.LogInformation("Outside");

        Output.ShouldBe(Lf("""
                           11:00:00.000 INF MyApp.Services.OrderService => Scoped: Inside
                           11:00:00.000 INF MyApp.Services.OrderService: Outside

                           """));
    }

    [Test]
    public void Minimal_Never_Writes_Scopes()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.IncludeScopes = true;
        });

        using (logger.BeginScope("Scoped"))
            logger.LogInformation("Hello");

        Output.ShouldBe("INF Hello\n");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Detailed_Always_Writes_Scopes(bool includeScopes)
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.IncludeScopes = includeScopes;
        });

        using (logger.BeginScope("Outer"))
        using (logger.BeginScope("Inner"))
            logger.LogInformation("Hello");

        Output.ShouldContain("\n  Scopes:    Outer => Inner\n  Message:   Hello\n");
    }

    [Test]
    public void Detailed_Omits_Scopes_When_None_Are_Active()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogInformation("Hello");

        Output.ShouldNotContain("Scopes:");
    }

    [TestCase(Category, "OrderService")]
    [TestCase("NoNamespace", "NoNamespace")]
    [TestCase("Trailing.", "Trailing.")]
    [TestCase("MyApp.Repository<MyApp.Models.Order>", "Repository<MyApp.Models.Order>")]
    public void UseShortCategoryName_Writes_The_Last_Segment(string category, string expected)
    {
        var logger = CreateLogger(c =>
            {
                c.Layout = FancyConsoleLayout.SingleLine;
                c.UseShortCategoryName = true;
            },
            category);

        logger.LogInformation("Hello");

        Output.ShouldBe($"11:00:00.000 INF {expected}: Hello\n");
    }

    [Test]
    public void UseShortCategoryName_Applies_To_Standard_And_Detailed()
    {
        var logger = CreateLogger(c => c.UseShortCategoryName = true);
        logger.LogInformation("Hello");
        Output.ShouldStartWith("2020-01-01 +11:00 OrderService\n");

        TearDownApp();
        SetUpConsoles();

        logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.UseShortCategoryName = true;
        });

        logger.LogInformation("Hello");
        Output.ShouldContain("\n  Category:  OrderService\n");
    }
}
