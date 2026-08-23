namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// Finds the creator-editable content files and adds them to configuration.
/// </summary>
/// <remarks>
/// They are ordinary JSON configuration sources rather than something hand-parsed, which is
/// what gives the content validation at startup, a clear failure when it is wrong, and reload
/// when the creator saves an edit while the API is running.
/// <para>
/// Content lives in <c>server/content/</c> so it sits beside the code it is content for rather
/// than inside the API project's own settings. The repository path is preferred over the
/// copied-to-output one so that editing the file a person can see is the edit that takes
/// effect during development.
/// </para>
/// </remarks>
internal static class GameContentFiles
{
    public const string DirectoryName = "content";

    public const string UnitsFile = "units.json";

    public const string WeaponsFile = "weapons.json";

    /// <summary>Adds every content file, failing immediately if one is missing.</summary>
    public static IConfigurationBuilder AddWeaponsOfOrderContent(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment)
        => configuration
            .AddContentFile(environment, UnitsFile)
            .AddContentFile(environment, WeaponsFile);

    private static IConfigurationBuilder AddContentFile(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment,
        string fileName)
        // Not optional: content missing is a broken deployment, and a game with no Units is
        // not a state worth starting in and discovering later.
        => configuration.AddJsonFile(Resolve(environment, fileName), optional: false, reloadOnChange: true);

    /// <summary>The first candidate path that exists.</summary>
    /// <exception cref="InvalidOperationException">No candidate exists.</exception>
    public static string Resolve(IHostEnvironment environment, string fileName)
    {
        var candidates = Candidates(environment, fileName).ToArray();
        var found = Array.Find(candidates, File.Exists);

        return found ?? throw new InvalidOperationException(
            $"Game content file '{fileName}' was not found. Looked in: {string.Join(", ", candidates)}.");
    }

    private static IEnumerable<string> Candidates(IHostEnvironment environment, string fileName)
    {
        // Development and tests: the API is rooted at server/src/WeaponsOfOrder.Api, so the
        // repository's own content directory is two levels up. This is the live file.
        yield return Full(environment.ContentRootPath, "..", "..", DirectoryName, fileName);

        // Published: the project copies the directory beside the application.
        yield return Full(environment.ContentRootPath, DirectoryName, fileName);
        yield return Full(AppContext.BaseDirectory, DirectoryName, fileName);
    }

    private static string Full(string root, params string[] parts)
        => Path.GetFullPath(Path.Combine([root, .. parts]));
}
