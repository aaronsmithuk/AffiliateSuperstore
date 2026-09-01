namespace AffiliateSuperstore.Core.Legal;

public sealed class LegalNoticeOptions
{
    public const string SectionName = "Legal";

    public string OperatorName { get; set; } = string.Empty;

    public string TradingName { get; set; } = string.Empty;

    public string LegalForm { get; set; } = string.Empty;

    public string GeographicAddress { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public string? CompanyNumber { get; set; }

    public string? CompanyRegistrationJurisdiction { get; set; }

    public string? VatNumber { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(TradingName) ? OperatorName : TradingName;

    public void ValidateForProduction()
    {
        var missing = new List<string>();
        Require(OperatorName, "Legal:OperatorName", missing);
        Require(TradingName, "Legal:TradingName", missing);
        Require(LegalForm, "Legal:LegalForm", missing);
        Require(GeographicAddress, "Legal:GeographicAddress", missing);
        Require(ContactEmail, "Legal:ContactEmail", missing);
        Require(Telephone, "Legal:Telephone", missing);

        if (!string.IsNullOrWhiteSpace(CompanyNumber) && string.IsNullOrWhiteSpace(CompanyRegistrationJurisdiction))
        {
            missing.Add("Legal:CompanyRegistrationJurisdiction (required when CompanyNumber is set)");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Production legal disclosures are incomplete. Configure: " + string.Join(", ", missing) + ".");
        }
    }

    private static void Require(string value, string name, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value)) missing.Add(name);
    }
}
