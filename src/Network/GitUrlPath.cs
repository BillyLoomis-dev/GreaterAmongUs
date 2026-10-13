namespace BetterAmongUs.Network;

/// <summary>
/// Represents a Git URL path for accessing BetterAmongUs resources.
/// </summary>
/// <param name="folder">The folder path within the repository.</param>
internal struct GitUrlPath(string folder)
{
    /// <summary>
    /// The base URL for the GitHub raw content.
    /// </summary>
    private const string BASE_URL = "https://raw.githubusercontent.com/D1GQ/BetterAmongUs";

    /// <summary>
    /// The branch name for the repository.
    /// </summary>
    internal const string BRANCH = "main";

    /// <summary>
    /// Gets the URL path for the main repository folder.
    /// </summary>
    internal static readonly GitUrlPath Repository = new(BRANCH);

    /// <summary>
    /// Gets the URL path for the API folder.
    /// </summary>
    internal static readonly GitUrlPath RepositoryApi = new($"{BRANCH}/api");

    /// <summary>
    /// Gets the URL path for the news folder.
    /// </summary>
    internal static readonly GitUrlPath News = new($"{BRANCH}/api/news");

    private readonly string _folder = folder;

    /// <summary>
    /// Combines the base URL with additional path segments.
    /// </summary>
    /// <param name="paths">Additional path segments to append.</param>
    /// <returns>The complete URL string.</returns>
    internal readonly string Combine(params string[] paths)
    {
        return $"{BASE_URL}/{_folder}/{string.Join("/", paths)}";
    }

    /// <summary>
    /// Returns the base URL for this path.
    /// </summary>
    /// <returns>The complete base URL string.</returns>
    public override readonly string ToString()
    {
        return $"{BASE_URL}/{_folder}";
    }
}