using System.Security.Cryptography;
using System.Text;
using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.Models;

public sealed record SourceSnapshotMetadata
{
    public SourceSnapshotMetadata(
        string source,
        DateTime capturedAtUtc,
        int recordCount,
        string correlationId,
        string payloadHash)
    {
        Source = Guard.NotNullOrWhiteSpace(source, nameof(source));
        CapturedAtUtc = Guard.Utc(capturedAtUtc, nameof(capturedAtUtc));
        RecordCount = Guard.Positive(recordCount, nameof(recordCount));
        CorrelationId = Guard.NotNullOrWhiteSpace(correlationId, nameof(correlationId));
        PayloadHash = Guard.NotNullOrWhiteSpace(payloadHash, nameof(payloadHash));
    }

    public string Source { get; }

    public DateTime CapturedAtUtc { get; }

    public int RecordCount { get; }

    public string CorrelationId { get; }

    public string PayloadHash { get; }

    public static string ComputePayloadHash(string payload)
    {
        var normalizedPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload));
        var bytes = Encoding.UTF8.GetBytes(normalizedPayload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}