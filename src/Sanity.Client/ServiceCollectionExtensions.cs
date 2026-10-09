using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sanity.Client;

/// <summary>
/// Dependency-injection registration helpers for <see cref="ISanityClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISanityClient"/> with options configured via a
    /// delegate. A dedicated named <see cref="HttpClient"/> is registered
    /// through <see cref="IHttpClientFactory"/>.
    /// </summary>
    public static IServiceCollection AddSanityClient(
        this IServiceCollection services,
        Action<SanityClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        return services.AddSanityClientCore();
    }

    /// <summary>
    /// Registers <see cref="ISanityClient"/> with options bound from the
    /// supplied <see cref="IConfiguration"/> section
    /// (defaults to the <c>"Sanity"</c> section, matching
    /// <see cref="SanityClientOptions.SectionName"/>).
    /// </summary>
    public static IServiceCollection AddSanityClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = SanityClientOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SanityClientOptions>(configuration.GetSection(sectionName));
        return services.AddSanityClientCore();
    }

    private static IServiceCollection AddSanityClientCore(this IServiceCollection services)
    {
        services.AddHttpClient<ISanityClient, SanityClient>();
        return services;
    }
}
