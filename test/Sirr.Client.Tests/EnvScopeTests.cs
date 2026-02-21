namespace Sirr.Tests;

public sealed class EnvScopeTests : IDisposable
{
    private const string TestKey1 = "SIRR_TEST_ENV_1";
    private const string TestKey2 = "SIRR_TEST_ENV_2";

    public EnvScopeTests()
    {
        // Clean state
        Environment.SetEnvironmentVariable(TestKey1, null);
        Environment.SetEnvironmentVariable(TestKey2, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(TestKey1, null);
        Environment.SetEnvironmentVariable(TestKey2, null);
    }

    [Fact]
    public void SetsEnvironmentVariables()
    {
        var secrets = new Dictionary<string, string>
        {
            [TestKey1] = "value1",
            [TestKey2] = "value2",
        };

        using var scope = new EnvScope(secrets);

        Assert.Equal("value1", Environment.GetEnvironmentVariable(TestKey1));
        Assert.Equal("value2", Environment.GetEnvironmentVariable(TestKey2));
    }

    [Fact]
    public void RestoresOriginalValues_OnDispose()
    {
        Environment.SetEnvironmentVariable(TestKey1, "original");

        var secrets = new Dictionary<string, string> { [TestKey1] = "override" };

        var scope = new EnvScope(secrets);
        Assert.Equal("override", Environment.GetEnvironmentVariable(TestKey1));

        scope.Dispose();
        Assert.Equal("original", Environment.GetEnvironmentVariable(TestKey1));
    }

    [Fact]
    public void RemovesNewVars_OnDispose()
    {
        Assert.Null(Environment.GetEnvironmentVariable(TestKey1));

        var secrets = new Dictionary<string, string> { [TestKey1] = "temp" };

        var scope = new EnvScope(secrets);
        Assert.Equal("temp", Environment.GetEnvironmentVariable(TestKey1));

        scope.Dispose();
        Assert.Null(Environment.GetEnvironmentVariable(TestKey1));
    }

    [Fact]
    public void DoubleDispose_IsSafe()
    {
        var secrets = new Dictionary<string, string> { [TestKey1] = "v" };
        var scope = new EnvScope(secrets);

        scope.Dispose();
        scope.Dispose(); // Should not throw
    }

    [Fact]
    public async Task AsyncDispose_RestoresValues()
    {
        Environment.SetEnvironmentVariable(TestKey1, "original");

        var secrets = new Dictionary<string, string> { [TestKey1] = "async-val" };

        await using (var scope = new EnvScope(secrets))
        {
            Assert.Equal("async-val", Environment.GetEnvironmentVariable(TestKey1));
        }

        Assert.Equal("original", Environment.GetEnvironmentVariable(TestKey1));
    }
}
