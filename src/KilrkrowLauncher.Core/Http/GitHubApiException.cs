namespace KilrkrowLauncher.Http;

public class GitHubApiException : Exception
{
    public int StatusCode { get; }

    public GitHubApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class GitHubRateLimitException : GitHubApiException
{
    public DateTimeOffset? ResetAt { get; }

    public GitHubRateLimitException(DateTimeOffset? resetAt, string message)
        : base(429, message)
    {
        ResetAt = resetAt;
    }

    public string UserMessage
    {
        get
        {
            if (ResetAt is { } reset)
            {
                var wait = reset - DateTimeOffset.UtcNow;
                if (wait < TimeSpan.Zero)
                    wait = TimeSpan.Zero;
                var minutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
                return "GitHub rate limit reached. Try again in about " + minutes + " minute(s), or add an optional token in Settings.";
            }

            return "GitHub rate limit reached. Try again later, or add an optional token in Settings.";
        }
    }
}
