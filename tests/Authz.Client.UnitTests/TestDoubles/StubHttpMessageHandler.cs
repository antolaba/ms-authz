using System.Net;
using System.Text;

namespace Authz.Client.UnitTests.TestDoubles;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> double for AuthzAdminClient tests — records the last
/// request made and returns a scripted response, without any real network call.
/// </summary>
public class StubHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return response;
    }
}
