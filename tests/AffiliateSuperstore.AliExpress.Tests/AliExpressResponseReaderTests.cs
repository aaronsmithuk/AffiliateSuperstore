namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AliExpressResponseReaderTests
{
    [Fact]
    public void ReadProducts_UnwrapsAffiliateEnvelopeAndNormalisesFields()
    {
        const string json = """
            {
              "aliexpress_affiliate_product_query_response": {
                "resp_result": {
                  "resp_code": 200,
                  "result": {
                    "current_page_no": 1,
                    "total_record_count": 42,
                    "products": {
                      "product": [{
                        "product_id": "1005001234567890",
                        "product_title": "Small green plush dragon",
                        "target_sale_price": "8.99",
                        "target_sale_price_currency": "GBP",
                        "commission_rate": "7%",
                        "lastest_volume": 135
                      }]
                    }
                  }
                }
              }
            }
            """;

        var page = AliExpressResponseReader.ReadProducts(json);

        var product = Assert.Single(page.Items);
        Assert.Equal("1005001234567890", product.ProductId);
        Assert.Equal("Small green plush dragon", product.Title);
        Assert.Equal("8.99", product.TargetSalePrice);
        Assert.Equal("GBP", product.Currency);
        Assert.Equal("7%", product.CommissionRate);
        Assert.Equal(135, product.RecentSalesVolume);
        Assert.Equal(42, page.TotalRecords);
    }

    [Fact]
    public void ReadPromotionLinks_SupportsNestedPromotionLinkArray()
    {
        const string json = """
            {"aliexpress_affiliate_link_generate_response":{"resp_result":{"result":{"promotion_links":{"promotion_link":[{"source_value":"https://www.aliexpress.com/item/1.html","promotion_link":"https://s.click.aliexpress.com/e/test"}]}}}}}
            """;

        var links = AliExpressResponseReader.ReadPromotionLinks(json);

        Assert.Equal("https://s.click.aliexpress.com/e/test", Assert.Single(links).PromotionUrl);
    }

    [Fact]
    public void ReadOrders_ParsesAmountsAndYNFlags()
    {
        const string json = """
            {"aliexpress_affiliate_order_list_response":{"resp_result":{"result":{"orders":{"order":[{"sub_order_id":"123","order_status":"Payment Completed","estimated_paid_commission":"1.25","incentive_commission_rate":"2%","estimated_incentive_paid_commission":"0.35","new_buyer_bonus_commission":"0.50","is_new_buyer":"Y","order_platform":"affiliate_platform","order_type":"global","is_affiliate_product":"Y","is_hot_product":"N"}]}}}}}
            """;

        var order = Assert.Single(AliExpressResponseReader.ReadOrders(json).Items);

        Assert.Equal(1.25m, order.EstimatedPaidCommission);
        Assert.True(order.IsAffiliateProduct);
        Assert.False(order.IsHotProduct);
        Assert.Equal("2%", order.IncentiveCommissionRate);
        Assert.Equal(.35m, order.EstimatedIncentivePaidCommission);
        Assert.Equal(.50m, order.NewBuyerBonusCommission);
        Assert.True(order.IsNewBuyer);
        Assert.Equal("affiliate_platform", order.OrderPlatform);
    }

    [Fact]
    public void ReadOrders_PreservesIndexCursorAndRawOrder()
    {
        const string json = """
            {"resp_result":{"result":{"min_query_index_id":"first","max_query_index_id":"next","orders":[{"sub_order_id":"456","order_status":"Completed Settlement"}]}}}
            """;

        var page = AliExpressResponseReader.ReadOrders(json);

        Assert.Equal("first", page.MinimumQueryIndexId);
        Assert.Equal("next", page.MaximumQueryIndexId);
        Assert.Contains("\"sub_order_id\":\"456\"", Assert.Single(page.Items).RawJson, StringComparison.Ordinal);
    }
}
