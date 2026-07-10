using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Api.Extensions;

/// <summary>
/// Uniform <see cref="Result"/> → HTTP translation. Success returns 200/201; failure maps the error
/// code to a status (404 / 409 / 400) and always returns the same body shape: <c>{ code, message }</c>,
/// which the frontend api-client reads directly for toasts.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess
            ? Results.Ok()
            : Problem(result.Error);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error);

    /// <summary>Use for successful creations that should answer 201 with a Location header.</summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, string location) =>
        result.IsSuccess
            ? Results.Created(location, result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        int statusCode = StatusCodeFor(error.Code);
        return Results.Json(new ErrorResponse(error.Code, error.Message), statusCode: statusCode);
    }

    private static int StatusCodeFor(string code)
    {
        // Convention: error code suffix drives the HTTP status. Modules stay HTTP-agnostic.
        if (code.EndsWith("NotFound", StringComparison.Ordinal))
            return StatusCodes.Status404NotFound;
        if (code.EndsWith("Conflict", StringComparison.Ordinal) ||
            code.EndsWith("AlreadyExists", StringComparison.Ordinal))
            return StatusCodes.Status409Conflict;

        return StatusCodes.Status400BadRequest;
    }

    private sealed record ErrorResponse(string Code, string Message);
}
