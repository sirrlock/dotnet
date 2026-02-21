using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sirr;

/// <summary>
/// Extension methods for registering <see cref="ISirrClient"/> with dependency injection.
/// </summary>
public static class SirrServiceCollectionExtensions
{
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

        return services.AddHttpClient<ISirrClient, SirrClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SirrOptions>>().Value;

            client.BaseAddress = new Uri(options.Server.TrimEnd('/'));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.Token);
        });
    }
}
