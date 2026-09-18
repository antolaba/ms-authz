using System.Collections.Concurrent;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsAuthz.Api.Authentication;

namespace MsAuthz.UnitTests.Api;

/// <summary>
/// El HEALTHCHECK del Dockerfile pega a /health (anonimo) cada 30 segundos. Si el handler devuelve
/// Fail cuando falta el header, cada uno de esos golpes escribe un fallo de autenticacion en el log
/// y el ruido tapa los rechazos que si importan.
/// </summary>
public class ApiKeyAuthenticationLoggingTests
{
    [Fact]
    public async Task An_anonymous_request_without_an_api_key_logs_no_authentication_failure()
    {
        using var factory = new LoggingApiFactory();
        var client = factory.CreateClient();

        await client.GetAsync("/health");

        factory.Logs.Should().NotContain(entry => entry.Contains("Failure message"));
    }

    [Fact]
    public async Task A_wrong_api_key_is_still_logged()
    {
        using var factory = new LoggingApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, "wrong-key");

        await client.PutAsJsonAsync("/users/u1/roles?tenant=jurol", new { roleCodes = Array.Empty<string>() });

        factory.Logs.Should().Contain(entry => entry.Contains("invalid API key"));
    }

    private sealed class LoggingApiFactory : MsAuthzApiFactory
    {
        public ConcurrentQueue<string> Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddSingleton<ILoggerProvider>(new QueueLoggerProvider(Logs)));
        }
    }

    private sealed class QueueLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new QueueLogger(sink);
        public void Dispose() { }

        private sealed class QueueLogger(ConcurrentQueue<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => sink.Enqueue(formatter(state, exception));
        }
    }
}
