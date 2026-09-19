using System.Text.Json.Serialization;

namespace KilrkrowLauncher.Catalog;

internal sealed class GitHubRepoDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    public bool Fork { get; set; }
    public bool Archived { get; set; }
}

internal sealed class GitHubReleaseDto
{
    public string TagName { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public List<GitHubAssetDto> Assets { get; set; } = [];
}

internal sealed class GitHubAssetDto
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
    public long Size { get; set; }
    public string? ContentType { get; set; }
}
