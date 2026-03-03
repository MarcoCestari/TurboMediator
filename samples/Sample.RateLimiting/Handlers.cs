using TurboMediator;

namespace Sample.RateLimiting;

// =============================================================
// MODELS
// =============================================================

public record AddressResult(
    string Cep, string Street, string Neighborhood,
    string City, string State);

public record FreightQuote(
    string Carrier, decimal Price, int EstimatedDays,
    string ServiceType);

public record TrackingResult(
    string TrackingCode, string Status, string Location,
    DateTime LastUpdate, TrackingEvent[] Events);

public record TrackingEvent(string Status, string Location, DateTime Timestamp);

public record ShipmentResult(
    string TrackingCode, string Status, string Carrier,
    DateTime EstimatedDelivery);

// =============================================================
// QUERY: Zip Code Lookup (FixedWindow rate limit)
// =============================================================

public record LookupAddressQuery(string Cep) : IQuery<AddressResult>;

public class LookupAddressHandler : IQueryHandler<LookupAddressQuery, AddressResult>
{
    private static readonly Dictionary<string, AddressResult> Addresses = new()
    {
        ["01310100"] = new("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP"),
        ["20040020"] = new("20040-020", "Av. Rio Branco", "Centro", "Rio de Janeiro", "RJ"),
        ["30130000"] = new("30130-000", "Av. Afonso Pena", "Centro", "Belo Horizonte", "MG"),
        ["80010000"] = new("80010-000", "R. XV de Novembro", "Centro", "Curitiba", "PR"),
    };

    public ValueTask<AddressResult> Handle(LookupAddressQuery query, CancellationToken ct)
    {
        var cep = query.Cep.Replace("-", "");
        if (Addresses.TryGetValue(cep, out var address))
            return new ValueTask<AddressResult>(address);

        return new ValueTask<AddressResult>(
            new AddressResult(query.Cep, "Unknown Street", "Centro", "São Paulo", "SP"));
    }
}

// =============================================================
// QUERY: Shipping Quote (SlidingWindow rate limit)
// =============================================================

public record CalculateFreightQuery(
    string OriginCep, string DestinationCep, double WeightKg) : IQuery<FreightQuote>;

public class CalculateFreightHandler : IQueryHandler<CalculateFreightQuery, FreightQuote>
{
    public ValueTask<FreightQuote> Handle(CalculateFreightQuery query, CancellationToken ct)
    {
        var baseCost = (decimal)query.WeightKg * 12.50m;
        var distance = Math.Abs(
            int.Parse(query.OriginCep.Replace("-", "")[..2]) -
            int.Parse(query.DestinationCep.Replace("-", "")[..2]));
        var price = baseCost + distance * 1.80m;
        var days = 2 + distance / 8;

        return new ValueTask<FreightQuote>(
            new FreightQuote("Correios PAC", Math.Round(price, 2), days, "Standard"));
    }
}

// =============================================================
// QUERY: Package Tracking (TokenBucket rate limit)
// =============================================================

public record TrackPackageQuery(string TrackingCode) : IQuery<TrackingResult>;

public class TrackPackageHandler : IQueryHandler<TrackPackageQuery, TrackingResult>
{
    public ValueTask<TrackingResult> Handle(TrackPackageQuery query, CancellationToken ct)
    {
        var events = new[]
        {
            new TrackingEvent("Package posted", "São Paulo - SP", DateTime.UtcNow.AddDays(-3)),
            new TrackingEvent("In transit", "Campinas - SP", DateTime.UtcNow.AddDays(-2)),
            new TrackingEvent("In transit", "Ribeirão Preto - SP", DateTime.UtcNow.AddDays(-1)),
            new TrackingEvent("Out for delivery", "Belo Horizonte - MG", DateTime.UtcNow),
        };

        return new ValueTask<TrackingResult>(
            new TrackingResult(query.TrackingCode, "In transit", "Belo Horizonte - MG",
                DateTime.UtcNow, events));
    }
}

// =============================================================
// COMMAND: Create Shipment (Bulkhead - concurrency limiter)
// =============================================================

public record CreateShipmentCommand(
    string SenderCep, string RecipientCep, double WeightKg,
    string RecipientName) : ICommand<ShipmentResult>;

public class CreateShipmentHandler : ICommandHandler<CreateShipmentCommand, ShipmentResult>
{
    public async ValueTask<ShipmentResult> Handle(CreateShipmentCommand cmd, CancellationToken ct)
    {
        // Simulates slow processing (carrier integration)
        await Task.Delay(500, ct);

        var trackingCode = $"BR{Random.Shared.NextInt64(100000000, 999999999)}BR";

        return new ShipmentResult(
            trackingCode,
            "Created",
            "Correios SEDEX",
            DateTime.UtcNow.AddDays(5));
    }
}
