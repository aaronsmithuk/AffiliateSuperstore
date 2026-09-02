namespace AffiliateSuperstore.Application.Orders;

public sealed class ConversionEvidenceOptions
{
    public const string SectionName = "ConversionEvidence";

    public Dictionary<string, ConversionEvidenceAcknowledgement> Acknowledgements { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ConversionEvidenceAcknowledgement
{
    public bool Confirmed { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTimeOffset? ConfirmedUtc { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
}
