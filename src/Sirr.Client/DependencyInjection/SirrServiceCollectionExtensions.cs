using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sirr;

/// <summary>
/// Extension methods for registering <see cref="ISirrClient"/> with dependency injection.
/// </summary>
public static class SirrServiceCollectionExtensions
{
    private const string HttpClientName = "SirrClient";

    /// <summary>
    /// Registers <see cref="ISirrClient"/> using <see cref="IHttpClientFactory"/>.
    /// Returns <see cref="IHttpClientBuilder"/> for chaining (e.g. Polly policies).
    /// </summary>
    public static IHttpClientBuilder AddSirrClient(
        this IServiceCollection services,
        Action<SirrOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.AddTransient<ISirrClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SirrOptions>>().Value;
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(HttpClientName);
            return new SirrClient(httpClient, options.Org);
        });

        return services.AddHttpClient(HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SirrOptions>>().Value;

            client.BaseAddress = new Uri(options.Server.TrimEnd('/'));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.Token);
        });
    }
}
