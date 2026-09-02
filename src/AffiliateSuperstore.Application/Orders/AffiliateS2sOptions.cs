namespace AffiliateSuperstore.Application.Orders;

public sealed class AffiliateS2sOptions
{
    public const string SectionName = "AliExpressS2s";
    public const int MinimumVerificationTokenCharacters = 32;
    public const int MaximumVerificationTokenCharacters = 512;
    public const int MinimumPayloadCharacters = 512;
    public const int MaximumPayloadCharactersLimit = 65536;

    public bool Enabled { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
    public int MaximumPayloadCharacters { get; set; } = 8192;

    public static void Validate(AffiliateS2sOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumPayloadCharacters is < MinimumPayloadCharacters or > MaximumPayloadCharactersLimit)
        {
            throw new InvalidOperationException(
                $"AliExpressS2s:MaximumPayloadCharacters must be between {MinimumPayloadCharacters} and {MaximumPayloadCharactersLimit}.");
        }

        if (!options.Enabled) return;

        if (string.IsNullOrWhiteSpace(options.VerificationToken) ||
            options.VerificationToken.Length is < MinimumVerificationTokenCharacters or > MaximumVerificationTokenCharacters)
        {
            throw new InvalidOperationException(
                $"AliExpressS2s:VerificationToken must contain between {MinimumVerificationTokenCharacters} and {MaximumVerificationTokenCharacters} characters when S2S is enabled.");
        }
    }
}
