namespace AffiliateSuperstore.Application.Orders;

public sealed class AffiliateS2sOptions
{
    public const string SectionName = "AliExpressS2s";

    public bool Enabled { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
    public int MaximumPayloadCharacters { get; set; } = 8192;
}
