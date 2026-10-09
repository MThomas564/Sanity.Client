using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sanity.Client.Presentation;

/// <summary>
/// Dependency-injection registration helpers for
/// <see cref="ISanityPresentationService"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISanityPresentationService"/>, scoped, with
    /// options configured via a delegate.
    /// </summary>
    public static IServiceCollection AddSanityVisualEditing(
        this IServiceCollection services,
        Action<SanityPresentationOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        return services.AddSanityVisualEditingCore();
    }

    /// <summary>
    /// Registers <see cref="ISanityPresentationService"/>, scoped, with
    /// options bound from the supplied <see cref="IConfiguration"/>
    /// section (defaults to <c>"SanityPresentation"</c>).
    /// </summary>
    public static IServiceCollection AddSanityVisualEditing(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = SanityPresentationOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SanityPresentationOptions>(configuration.GetSection(sectionName));
        return services.AddSanityVisualEditingCore();
    }

    private static IServiceCollection AddSanityVisualEditingCore(this IServiceCollection services)
    {
        services.AddScoped<ISanityPresentationService, SanityPresentationService>();
        return services;
    }
}
