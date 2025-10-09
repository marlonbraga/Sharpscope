using System.IO.Compression;

namespace Sharpscope.Api.Endpoints;

internal sealed class ZipWorkspace : IAsyncDisposable
{
    public DirectoryInfo RootDirectory { get; }
    public DirectoryInfo ExtractedDirectory { get; }

    private ZipWorkspace(DirectoryInfo root, DirectoryInfo extracted)
    {
        RootDirectory = root;
        ExtractedDirectory = extracted;
    }

    public static async Task<ZipWorkspace> CreateAsync(IFormFile zipFile, CancellationToken ct)
    {
        var root = new DirectoryInfo(Path.Combine(Path.GetTempPath(),
            "sharpscope_ws",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff") + "_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName())));
        root.Create();

        var zipPath = Path.Combine(root.FullName, "upload.zip");
        await using (var fs = File.Create(zipPath))
            await zipFile.CopyToAsync(fs, ct);

        var extracted = new DirectoryInfo(Path.Combine(root.FullName, "extracted"));
        extracted.Create();

        using var archive = ZipFile.OpenRead(zipPath);
        var basePath = Path.GetFullPath(extracted.FullName) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName)) continue;

            var targetPath = Path.GetFullPath(Path.Combine(extracted.FullName, entry.FullName));
            if (!targetPath.StartsWith(basePath, StringComparison.Ordinal)) continue; // ZipSlip guard

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var entryStream = entry.Open();
            await using var outStream = File.Create(targetPath);
            await entryStream.CopyToAsync(outStream, ct);
        }

        return new ZipWorkspace(root, extracted);
    }

    public ValueTask DisposeAsync()
    {
        try { if (RootDirectory.Exists) RootDirectory.Delete(true); } catch { /* best-effort */ }
        return ValueTask.CompletedTask;
    }
}
