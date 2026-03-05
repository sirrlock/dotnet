using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sirr.Tests.Helpers;

internal sealed class CapturedRequest
{
    public required HttpMethod Method { get; init; }
    public required Uri? RequestUri { get; init; }
    public required AuthenticationHeaderValue? Authorization { get; init; }
    public string? Body { get; init; }
}

internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<CapturedRequest> _captured = [];

    public IReadOnlyList<CapturedRequest> Requests => _captured;

    public void Enqueue(HttpStatusCode status, object body)
    {
        var json = JsonSerializer.Serialize(body);
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    public void EnqueueOk(object body) => Enqueue(HttpStatusCode.OK, body);

    public void EnqueueCreated(object body) => Enqueue(HttpStatusCode.Created, body);

    public void EnqueueNotFound() =>
        Enqueue(HttpStatusCode.NotFound, new { error = "not found" });

    public void EnqueueError(HttpStatusCode status, string message) =>
        Enqueue(status, new { error = message });

    public void EnqueueHead(HttpStatusCode status, Dictionary<string, string> headers)
    {
        var response = new HttpResponseMessage(status);
        foreach (var (k, v) in headers)
            response.Headers.TryAddWithoutValidation(k, v);
        _responses.Enqueue(response);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        _captured.Add(new CapturedRequest
        {
            Method = request.Method,
            RequestUri = request.RequestUri,
            Authorization = request.Headers.Authorization,
            Body = body,
        });

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No mock response queued for {request.Method} {request.RequestUri}");
        }

        return _responses.Dequeue();
    }
}
