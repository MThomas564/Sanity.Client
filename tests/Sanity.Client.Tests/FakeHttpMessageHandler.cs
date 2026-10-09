using System.Net;
using System.Text;

namespace Sanity.Client.Tests;

/// <summary>
/// A minimal fake <see cref="HttpMessageHandler"/> that records the last
/// request made and returns a canned response, for exercising
/// <see cref="Sanity.Client.SanityClient"/> without any network calls.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
        };

        return Task.FromResult(response);
    }
}
