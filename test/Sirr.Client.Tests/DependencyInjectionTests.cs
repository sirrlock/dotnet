using Microsoft.Extensions.DependencyInjection;

namespace Sirr.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddSirrClient_ResolvesISirrClient()
    {
        var services = new ServiceCollection();

        services.AddSirrClient(options =>
        {
            options.Server = "https://sirr.example.com";
            options.Token = "test-token";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ISirrClient>();

        Assert.NotNull(client);
        Assert.IsType<SirrClient>(client);
    }

    [Fact]
    public void AddSirrClient_ReturnsHttpClientBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddSirrClient(options =>
        {
            options.Server = "https://sirr.example.com";
            options.Token = "test-token";
        });

        Assert.NotNull(builder);
    }

    [Fact]
    public void AddSirrClient_ConfiguresHttpClient()
    {
        var services = new ServiceCollection();

        services.AddSirrClient(options =>
        {
            options.Server = "https://sirr.example.com";
            options.Token = "my-token";
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(nameof(ISirrClient));

        Assert.Equal(new Uri("https://sirr.example.com"), httpClient.BaseAddress);
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("my-token", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }
}
