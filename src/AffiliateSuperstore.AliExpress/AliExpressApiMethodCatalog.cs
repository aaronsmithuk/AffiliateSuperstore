namespace AffiliateSuperstore.AliExpress;

public enum AliExpressApiPermission
{
    Standard,
    AdditionalApproval,
    Advanced,
    SkuDimension,
    SystemTool
}

public sealed record AliExpressApiMethodDescriptor(
    string Method,
    string Name,
    string Group,
    AliExpressApiPermission Permission,
    string Description,
    bool IsSystemMethod = false);

public static class AliExpressApiMethodCatalog
{
    public static IReadOnlyList<AliExpressApiMethodDescriptor> All { get; } =
    [
        new("aliexpress.affiliate.category.get", "Categories", "Catalogue", AliExpressApiPermission.Standard,
            "Affiliate category IDs and names."),
        new("aliexpress.affiliate.product.query", "Product search", "Catalogue", AliExpressApiPermission.Standard,
            "Search affiliate products by keyword, category, price, delivery and popularity."),
        new("aliexpress.affiliate.productdetail.get", "Product details", "Catalogue", AliExpressApiPermission.Standard,
            "Retrieve details for up to 50 product IDs."),
        new("aliexpress.affiliate.link.generate", "Generate links", "Links", AliExpressApiPermission.Standard,
            "Convert up to 50 AliExpress URLs into tracked promotion links."),
        new("aliexpress.affiliate.featuredpromo.get", "Featured promotions", "Promotions", AliExpressApiPermission.Standard,
            "List active featured promotion campaigns."),
        new("aliexpress.affiliate.featuredpromo.products.get", "Featured promotion products", "Promotions", AliExpressApiPermission.Standard,
            "List products within a named featured promotion."),
        new("aliexpress.affiliate.promotion.info.get", "Coupon information", "Promotions", AliExpressApiPermission.AdditionalApproval,
            "Retrieve current applicable coupon information for up to 10 products. App 6102 currently receives InsufficientPermission."),
        new("aliexpress.affiliate.hotproduct.query", "Hot product search", "Advanced", AliExpressApiPermission.Advanced,
            "Search AliExpress hot products."),
        new("aliexpress.affiliate.hotproduct.download", "Hot product download", "Advanced", AliExpressApiPermission.Advanced,
            "Legacy category-based hot product retrieval."),
        new("aliexpress.affiliate.product.smartmatch", "Smart match", "Advanced", AliExpressApiPermission.Advanced,
            "Request product recommendations by product or keywords."),
        new("aliexpress.affiliate.product.shipping.get", "Shipping information", "Advanced", AliExpressApiPermission.Advanced,
            "Retrieve product/SKU shipping cost and delivery estimates."),
        new("aliexpress.affiliate.product.sku.detail.get", "SKU details", "Advanced", AliExpressApiPermission.SkuDimension,
            "Retrieve price, specification and optional delivery data for up to 20 SKUs."),
        new("aliexpress.affiliate.order.get", "Orders by ID", "Orders", AliExpressApiPermission.Standard,
            "Retrieve affiliate sub-orders by ID."),
        new("aliexpress.affiliate.order.list", "Order list", "Orders", AliExpressApiPermission.Standard,
            "Page through affiliate orders by time and status."),
        new("aliexpress.affiliate.order.listbyindex", "Order list by index", "Orders", AliExpressApiPermission.Standard,
            "Incrementally page through affiliate orders by query index."),
        new("/aliexpress/xinghe/merchant/license/get", "Merchant business licence", "System", AliExpressApiPermission.SystemTool,
            "Retrieve merchant licence information for a seller sequence.", true)
    ];
}
