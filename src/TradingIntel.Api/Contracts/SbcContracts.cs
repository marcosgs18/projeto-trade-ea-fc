namespace TradingIntel.Api.Contracts;

public sealed record SbcChallengeResponse(
    Guid Id,
    string Title,
    string Category,
    DateTime? ExpiresAtUtc,
    string RepeatabilityKind,
    int? RepeatabilityMaxCompletions,
    string SetName,
    DateTime ObservedAtUtc,
    IReadOnlyList<SbcRequirementResponse> Requirements);

public sealed record SbcRequirementResponse(string Key, int Minimum, int? Maximum);
