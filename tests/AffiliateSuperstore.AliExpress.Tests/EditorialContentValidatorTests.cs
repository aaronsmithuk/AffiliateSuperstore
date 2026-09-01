using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class EditorialContentValidatorTests
{
    private readonly EditorialContentValidator _validator = new();

    [Fact]
    public void Validate_PassesUsefulCopyWithoutUnsupportedClaims()
    {
        var result = _validator.Validate(new EditorialValidationInput(
            "Highland cattle plush toy 40cm cotton",
            "Highland Cow Plush 40cm",
            "A cotton Highland cow with a friendly expression, rounded shape and gentle styling for a cheerful collectable display."));

        Assert.Equal(EditorialValidationState.Passed, result.State);
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("Official Highland cow plush", "claim.authenticity")]
    [InlineData("Highland cow plush with fast delivery", "claim.delivery")]
    [InlineData("The best Highland cow plush", "claim.superlative")]
    [InlineData("Highland cow plush with 5-star reviews", "claim.performance")]
    public void Validate_BlocksRestrictedEditorialClaims(string title, string expectedCode)
    {
        var result = _validator.Validate(new EditorialValidationInput(
            "Highland cattle plush toy",
            title,
            "A softly styled character with rounded features and a friendly expression for a cheerful collectable display."));

        Assert.Equal(EditorialValidationState.Blocked, result.State);
        Assert.Contains(result.Findings, finding => finding.Code == expectedCode);
    }

    [Fact]
    public void Validate_WarnsWhenDescriptionIsTooThin()
    {
        var result = _validator.Validate(new EditorialValidationInput(
            "Highland cattle plush toy", "Highland Cow Plush", "Friendly and soft."));

        Assert.Equal(EditorialValidationState.Warning, result.State);
        Assert.Contains(result.Findings, finding => finding.Code == "copy.description-thin");
    }

    [Fact]
    public void Validate_BlocksPromotionalSourceNarrationFromHighlandCowPilot()
    {
        var result = _validator.Validate(new EditorialValidationInput(
            "Adorable Highland Cattle Plush Toy 45cm - Huggable Running Cow Stuffed Animal Made with Premium Soft Fabric, Soothing Companion",
            "45cm Highland Cattle Plush Toy",
            "A 45cm Highland cattle plush toy described as huggable and made with premium soft fabric. The source title also describes it as a running cow stuffed animal and soothing companion."));

        Assert.Equal(EditorialValidationState.Blocked, result.State);
        Assert.Contains(result.Findings, finding => finding.Code == "copy.promotional-language");
        Assert.Contains(result.Findings, finding => finding.Code == "copy.source-narration");
    }
}
