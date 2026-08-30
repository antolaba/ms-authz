namespace MsAuthz.Application.Common.Exceptions;

/// <summary>
/// Base type for the handful of application-level failures ms-authz needs to distinguish.
/// Mirrors the shape (not the machinery) of estudio-contable-backend's AppException: a small, closed
/// set of subtypes the Api layer maps to HTTP status codes. There is no BusinessCodes catalog here —
/// this service does not talk to end users, only to other backends.
/// </summary>
public abstract class MsAuthzException(string message) : Exception(message);

/// <summary>The caller asked for something structurally invalid (e.g. an unknown role code in a PUT).</summary>
public sealed class InvalidCatalogRequestException(string message) : MsAuthzException(message);
