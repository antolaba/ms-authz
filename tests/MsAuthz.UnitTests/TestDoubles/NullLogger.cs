using Microsoft.Extensions.Logging.Abstractions;

namespace MsAuthz.UnitTests.TestDoubles;

/// <summary>Shorthand so test setup doesn't repeat the generic NullLogger&lt;T&gt;.Instance everywhere.</summary>
public static class TestLogger
{
    public static Microsoft.Extensions.Logging.ILogger<T> For<T>() => NullLogger<T>.Instance;
}
