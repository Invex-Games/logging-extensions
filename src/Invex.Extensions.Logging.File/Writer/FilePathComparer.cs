namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     Compares normalized file paths using the destination directories' actual casing behavior.
///     Instances cache observations for one batch or rollover/retention operation.
/// </summary>
/// <param name="fileSystem">The file system on which the paths will be used.</param>
internal sealed class FilePathComparer(IFileSystem fileSystem) : IEqualityComparer<string>
{
    /// <summary>
    ///     Directory identity and filename casing observations, keyed by exact directory spellings.
    /// </summary>
    private readonly Dictionary<(string Left, string Right), (bool SameDirectory, bool IgnoreCase)>
        _directories = new();

    /// <inheritdoc />
    public bool Equals(string? x, string? y)
    {
        if (StringComparer.Ordinal.Equals(x, y))
            return true;

        if (x is null || y is null || !StringComparer.OrdinalIgnoreCase.Equals(x, y))
            return false;

        var leftDirectory = fileSystem.Path.GetDirectoryName(x)!;
        var rightDirectory = fileSystem.Path.GetDirectoryName(y)!;
        var key = (leftDirectory, rightDirectory);

        if (!_directories.TryGetValue(key, out var comparison))
        {
            // Missing inactive destinations cannot alias a file in an existing directory.
            if (!fileSystem.Directory.Exists(leftDirectory) || !fileSystem.Directory.Exists(rightDirectory))
                return false;

            comparison = ProbeDirectories(leftDirectory, rightDirectory);
            _directories.Add(key, comparison);
        }

        return comparison.SameDirectory &&
               (comparison.IgnoreCase ||
                StringComparer.Ordinal.Equals(fileSystem.Path.GetFileName(x), fileSystem.Path.GetFileName(y)));
    }

    /// <inheritdoc />
    public int GetHashCode(string obj) =>
        // Case-sensitive destinations may share a hash, but equality still keeps them separate.
        StringComparer.OrdinalIgnoreCase.GetHashCode(obj);

    /// <summary>
    ///     Uses a temporary, exclusively created filename to observe directory aliases and case sensitivity.
    ///     Checking directory identity separately preserves distinct case-sensitive ancestor directories.
    /// </summary>
    /// <param name="leftDirectory">An existing writable destination directory.</param>
    /// <param name="rightDirectory">The directory spelling to compare with the destination.</param>
    /// <returns>Whether the directories coincide and filenames in that directory ignore case.</returns>
    private (bool SameDirectory, bool IgnoreCase) ProbeDirectories(string leftDirectory, string rightDirectory)
    {
        var probeName = $".invex-path-{Guid.NewGuid():N}.tmp";
        var probePath = fileSystem.Path.Combine(leftDirectory, probeName);
        var created = false;

        try
        {
            using var probe = fileSystem.File.Open(probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);

            created = true;

            var sameDirectory = fileSystem.File.Exists(fileSystem.Path.Combine(rightDirectory, probeName));

            var ignoreCase =
                fileSystem.File.Exists(fileSystem.Path.Combine(leftDirectory, probeName.ToUpperInvariant()));

            return (sameDirectory, ignoreCase);
        }
        finally
        {
            if (created)
                fileSystem.File.Delete(probePath);
        }
    }
}
