using System.Net;
using System.Text;

namespace Authz.Client.UnitTests.TestDoubles;

public class SequencedHttpMessageHandler(params (HttpStatusCode StatusCode, string? JsonBody)[] responses) : HttpMessageHandler
{
    private int _index;

    public List<Uri> RequestedUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);

        var (statusCode, jsonBody) = responses[Math.Min(_index, responses.Length - 1)];
        _index++;

        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return Task.FromResult(response);
    }
}
