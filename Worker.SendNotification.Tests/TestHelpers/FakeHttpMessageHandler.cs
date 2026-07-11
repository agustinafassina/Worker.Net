using System.Net;

namespace Worker.SendNotification.Tests.TestHelpers;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "ok")
        : this((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        }))
    {
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await _handler(request, cancellationToken);
    }
}
