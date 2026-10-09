using Microsoft.Extensions.DependencyInjection;

namespace Sanity.Client.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSanityClient_ResolvesISanityClient_ThroughActivatorUtilities()
    {
        // Regression test: AddHttpClient<ISanityClient, SanityClient>() uses
        // ActivatorUtilities internally, which throws MultipleCtorsFound if
        // SanityClient has more than one constructor ActivatorUtilities could
        // apply (HttpClient + IOptions<SanityClientOptions> are both always
        // resolvable). This exercises the exact path a Blazor/ASP.NET Core
        // app hits when resolving ISanityClient from DI.
        var services = new ServiceCollection();
        services.AddSanityClient(options =>
        {
            options.ProjectId = "test123";
            options.Dataset = "production";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ISanityClient>();

        Assert.IsType<SanityClient>(client);
    }
}
