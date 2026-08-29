using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Exceptions;

namespace MsAuthz.Api.Extensions;

/// <summary>
/// Small RFC 7807 translator, same shape as estudio-contable-backend's GlobalExceptionHandler but for
/// ms-authz's much smaller exception set. There is no BusinessCodes catalog here — ms-authz's callers
/// are backends, not end users, so a Title + a Type is enough.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            CatalogEntryNotFoundException => (StatusCodes.Status404NotFound, "Catalog entry not found"),
            InvalidCatalogRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "{Title} while processing {Path}", title, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
