using System.Net;
using System.Text;

namespace Sanity.Client.Tests;

/// <summary>
/// A minimal fake <see cref="HttpMessageHandler"/> that records the last
/// request made (including its body) and returns a canned response, for
/// exercising <see cref="Sanity.Client.SanityClient"/> without any
/// network calls.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    /// <summary>If set, the handler waits this long before responding -- used to simulate a slow/unresponsive server.</summary>
    public TimeSpan? Delay { get; set; }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (Delay is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
        };

        return response;
    }
}
