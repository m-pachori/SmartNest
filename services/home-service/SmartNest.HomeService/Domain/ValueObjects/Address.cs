namespace SmartNest.HomeService.Domain.ValueObjects;

/// <summary>Postal address value object for a <see cref="Home"/>.</summary>
public sealed record Address(string Street, string City, string State, string PostalCode, string Country)
{
    public string Street { get; init; } = Require(Street, nameof(Street));

    public string City { get; init; } = Require(City, nameof(City));

    public string State { get; init; } = Require(State, nameof(State));

    public string PostalCode { get; init; } = Require(PostalCode, nameof(PostalCode));

    public string Country { get; init; } = Require(Country, nameof(Country));

    private static string Require(string value, string paramName) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{paramName} is required.", paramName);
}
