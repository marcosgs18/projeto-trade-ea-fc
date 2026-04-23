using System.Text;
using System.Text.Json;
using TradingIntel.Application.Snapshots;
using TradingIntel.Domain.Models;

namespace TradingIntel.Infrastructure.Snapshots;

public sealed class FileRawSnapshotStore : IRawSnapshotStore
{
    private readonly string _rootPath;

    public FileRawSnapshotStore(string baseDirectory)
    {
        _rootPath = Path.Combine(baseDirectory, "data", "snapshots");
    }

    public async Task SaveAsync(SourceSnapshotMetadata metadata, string rawPayload, CancellationToken cancellationToken)
    {
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));
        if (rawPayload is null) throw new ArgumentNullException(nameof(rawPayload));

        var day = metadata.CapturedAtUtc.ToString("yyyyMMdd");
        var dir = Path.Combine(_rootPath, metadata.Source, day, metadata.CorrelationId);
        Directory.CreateDirectory(dir);

        var payloadPath = Path.Combine(dir, "payload.txt");
        var metadataPath = Path.Combine(dir, "metadata.json");

        await File.WriteAllTextAsync(payloadPath, rawPayload, Encoding.UTF8, cancellationToken);

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, Encoding.UTF8, cancellationToken);
    }
}

