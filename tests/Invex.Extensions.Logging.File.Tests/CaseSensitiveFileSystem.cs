namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Provides an in-memory file system whose filename comparison is independent of its reported separator.
/// </summary>
internal sealed class CaseSensitiveFileSystem
{
    /// <summary>
    ///     Controls whether differently cased names address the same storage entry.
    /// </summary>
    private readonly bool _caseSensitive;

    /// <summary>
    ///     Tracks directories using the selected comparison rules.
    /// </summary>
    private readonly HashSet<string> _directories;

    /// <summary>
    ///     Preserves the original spelling of filenames returned by directory enumeration.
    /// </summary>
    private readonly Dictionary<string, string> _paths;

    /// <summary>
    ///     Stores file content and timestamps without using the real disk.
    /// </summary>
    private readonly MockFileSystem _storage = new();

    /// <summary>
    ///     Initializes the mock operations exercised by the writers and their path comparer.
    /// </summary>
    /// <param name="caseSensitive">Whether case distinguishes files and directories.</param>
    /// <param name="separator">The separator reported by the filesystem facade.</param>
    public CaseSensitiveFileSystem(bool caseSensitive, char separator)
    {
        _caseSensitive = caseSensitive;

        var comparer = caseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        _paths = new(comparer);

        _directories = new(comparer)
        {
            _storage.Directory.GetCurrentDirectory(),
        };

        FileSystem = A.Fake<IFileSystem>();
        var path = A.Fake<IPath>(options => options.Wrapping(_storage.Path));
        var file = A.Fake<IFile>();
        var directory = A.Fake<IDirectory>();
        var fileInfo = A.Fake<IFileInfoFactory>();

        A
            .CallTo(() => FileSystem.Path)
            .Returns(path);

        A
            .CallTo(() => FileSystem.File)
            .Returns(file);

        A
            .CallTo(() => FileSystem.Directory)
            .Returns(directory);

        A
            .CallTo(() => FileSystem.FileInfo)
            .Returns(fileInfo);

        A
            .CallTo(() => path.DirectorySeparatorChar)
            .Returns(separator);

        A
            .CallTo(() => directory.GetCurrentDirectory())
            .Returns(_storage.Directory.GetCurrentDirectory());

        A
            .CallTo(() => directory.Exists(A<string>._))
            .ReturnsLazily((string value) => _directories.Contains(path.GetFullPath(value)));

        A
            .CallTo(() => directory.CreateDirectory(A<string>._))
            .ReturnsLazily((string value) =>
            {
                _directories.Add(path.GetFullPath(value));

                return _storage.Directory.CreateDirectory(value);
            });

        A
            .CallTo(() => directory.GetFiles(A<string>._, A<string>._))
            .ReturnsLazily((string value, string pattern) => _paths
                .Values
                .Where(name => comparer.Equals(path.GetDirectoryName(name), path.GetFullPath(value)))
                .Where(name => pattern == "*" || comparer.Equals(path.GetExtension(name), ".log"))
                .ToArray());

        A
            .CallTo(() => file.Exists(A<string>._))
            .ReturnsLazily((string value) => _storage.File.Exists(GetStoragePath(value)));

        A
            .CallTo(() => file.Open(A<string>._, A<FileMode>._, A<FileAccess>._, A<FileShare>._))
            .ReturnsLazily((string value, FileMode mode, FileAccess access, FileShare share) =>
                _storage.File.Open(GetStoragePath(value, true), mode, access, share));

        A
            .CallTo(() => file.AppendText(A<string>._))
            .ReturnsLazily((string value) => _storage.File.AppendText(GetStoragePath(value, true)));

        A
            .CallTo(() => file.ReadAllText(A<string>._))
            .ReturnsLazily((string value) => _storage.File.ReadAllText(GetStoragePath(value)));

        A
            .CallTo(() => file.Delete(A<string>._))
            .Invokes((string value) =>
            {
                _storage.File.Delete(GetStoragePath(value));
                _paths.Remove(path.GetFullPath(value));
            });

        A
            .CallTo(() => file.Move(A<string>._, A<string>._))
            .Invokes((string source, string destination) =>
            {
                _storage.File.Move(GetStoragePath(source), GetStoragePath(destination, true));
                _paths.Remove(path.GetFullPath(source));
            });

        A
            .CallTo(() => fileInfo.New(A<string>._))
            .ReturnsLazily((string value) => _storage.FileInfo.New(GetStoragePath(value)));
    }

    /// <summary>
    ///     Gets the filesystem passed to production writers.
    /// </summary>
    public IFileSystem FileSystem { get; }

    /// <summary>
    ///     Gets all remaining filenames, including any leaked probe files.
    /// </summary>
    public IEnumerable<string> AllFiles => _paths.Values;

    /// <summary>
    ///     Resolves a filename inside the fixture's log directory.
    /// </summary>
    /// <param name="name">The filename, including its extension.</param>
    /// <returns>The normalized log path.</returns>
    public string LogPath(string name) =>
        FileSystem.Path.GetFullPath(FileSystem.Path.Combine("Logs", name));

    /// <summary>
    ///     Seeds a log file with deterministic content and timestamps.
    /// </summary>
    /// <param name="name">The filename, including its extension.</param>
    /// <param name="content">The initial file content.</param>
    public void AddFile(string name, string content)
    {
        var path = LogPath(name);
        FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(path)!);
        var timestamp = new TestTimeProvider().GetUtcNow();

        _storage.AddFile(GetStoragePath(path, true),
            new(content)
            {
                CreationTime = timestamp,
                LastWriteTime = timestamp,
                LastAccessTime = timestamp,
            });
    }

    /// <summary>
    ///     Encodes names so the host platform cannot merge case-sensitive mock destinations.
    /// </summary>
    /// <param name="path">The virtual path.</param>
    /// <param name="remember">Whether to register the original filename for enumeration.</param>
    /// <returns>The corresponding in-memory backing path.</returns>
    private string GetStoragePath(string path, bool remember = false)
    {
        path = FileSystem.Path.GetFullPath(path);

        if (remember && !_paths.ContainsKey(path))
            _paths.Add(path, path);

        var key = _caseSensitive
            ? path
            : path.ToUpperInvariant();

        var name = BitConverter
            .ToString(Encoding.UTF8.GetBytes(key))
            .Replace("-", string.Empty);

        return _storage.Path.Combine(_storage.Directory.GetCurrentDirectory(), name);
    }
}
