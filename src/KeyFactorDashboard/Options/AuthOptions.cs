namespace KeyFactorDashboard.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "KeyFactor";
    public string Audience { get; set; } = "KeyFactor";
    public string SigningKey { get; set; } = "plant-edition-dev-signing-key-32b!";
    public int LifetimeHours { get; set; } = 12;
}
