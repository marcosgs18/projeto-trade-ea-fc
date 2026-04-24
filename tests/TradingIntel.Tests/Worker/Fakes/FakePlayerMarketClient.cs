using TradingIntel.Application.PlayerMarket;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Tests.Worker.Fakes;

internal sealed class FakePlayerMarketClient : IPlayerMarketClient
{
    public int CallCount { get; private set; }

    public List<PlayerReference> Requests { get; } = new();

    public Func<PlayerReference, CancellationToken, Task<PlayerMarketSnapshot>>? Handler { get; set; }

    public Task<PlayerMarketSnapshot> GetPlayerMarketSnapshotAsync(PlayerReference player, CancellationToken cancellationToken)
    {
        CallCount++;
        Requests.Add(player);

        if (Handler is null)
        {
            return Task.FromResult(BuildDefaultSnapshot(player));
        }

        return Handler(player, cancellationToken);
    }

    public static PlayerMarketSnapshot BuildDefaultSnapshot(PlayerReference player)
    {
        var capturedAt = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        var prices = new[]
        {
            new PlayerPriceSnapshot(
                player,
                source: "futgg:pc",
                capturedAtUtc: capturedAt,
                buyNowPrice: new Coins(10_000m),
                sellNowPrice: new Coins(11_000m),
                medianMarketPrice: new Coins(10_500m)),
        };

        var listings = new[]
        {
            new MarketListingSnapshot(
                listingId: $"listing-{player.PlayerId}-1",
                player,
                source: "futgg:pc",
                capturedAtUtc: capturedAt,
                startingBid: new Coins(9_500m),
                buyNowPrice: new Coins(10_000m),
                expiresAtUtc: capturedAt.AddMinutes(30)),
        };

        return new PlayerMarketSnapshot(
            Source: "futgg:pc",
            CapturedAtUtc: capturedAt,
            CorrelationId: Guid.NewGuid().ToString("N"),
            RawPayload: "raw-fixture",
            PriceSnapshots: prices,
            LowestListingSnapshots: listings);
    }
}
