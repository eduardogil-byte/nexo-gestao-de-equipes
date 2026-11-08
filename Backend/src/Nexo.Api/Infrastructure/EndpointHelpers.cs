using Nexo.Api.Domain;

namespace Nexo.Api.Infrastructure;

public static class EndpointHelpers
{
    public static IResult Error(string message, int code = 400) => Results.Json(new { error = message }, statusCode: code);
    public static bool IsValid(string? text, int max, int min = 1) =>
        !string.IsNullOrWhiteSpace(text) && text.Trim().Length >= min && text.Trim().Length <= max;
    public static bool IsHttpLink(string? url) => string.IsNullOrWhiteSpace(url) ||
        (url.Length <= 2048 && Uri.TryCreate(url, UriKind.Absolute, out var u) &&
         (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps));
    public static bool IsEditable(ResearchProject p) => p.Status is not (ProjectState.Cancelled or ProjectState.Completed);
}
