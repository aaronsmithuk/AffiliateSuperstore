using System.Globalization;
using System.Text.Json;

namespace AffiliateSuperstore.AliExpress;

public static class AliExpressResponseReader
{
    public static IReadOnlyList<AliExpressCategory> ReadCategories(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = GetResult(document.RootElement);
        var items = GetNestedArray(result, "categories", "category");

        return items
            .Select(item => new AliExpressCategory(
                GetString(item, "category_id") ?? string.Empty,
                GetString(item, "category_name") ?? string.Empty,
                GetString(item, "parent_category_id")))
            .Where(category => category.CategoryId.Length > 0)
            .ToArray();
    }

    public static AliExpressPage<AliExpressProduct> ReadProducts(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = GetResult(document.RootElement);
        var items = GetNestedArray(result, "products", "product");

        return new AliExpressPage<AliExpressProduct>(
            items.Select(ReadProduct).Where(product => product.ProductId.Length > 0).ToArray(),
            GetInt(result, "current_page_no"),
            GetInt(result, "total_page_no"),
            GetInt(result, "total_record_count"));
    }

    public static IReadOnlyList<AliExpressPromotionLink> ReadPromotionLinks(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = GetResult(document.RootElement);
        var items = GetNestedArray(result, "promotion_links", "promotion_link");

        return items
            .Select(item => new AliExpressPromotionLink(
                GetString(item, "source_value") ?? string.Empty,
                GetString(item, "promotion_link") ?? string.Empty,
                GetString(item, "message")))
            .Where(link => link.PromotionUrl.Length > 0)
            .ToArray();
    }

    public static IReadOnlyList<AliExpressFeaturedPromotion> ReadFeaturedPromotions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = GetResult(document.RootElement);
        var items = GetNestedArray(result, "promos", "promo");

        return items
            .Select(item => new AliExpressFeaturedPromotion(
                GetString(item, "promo_name") ?? string.Empty,
                GetString(item, "promo_desc"),
                GetInt(item, "product_num")))
            .Where(promotion => promotion.Name.Length > 0)
            .ToArray();
    }

    public static AliExpressPage<AliExpressOrder> ReadOrders(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = GetResult(document.RootElement);
        var items = GetNestedArray(result, "orders", "order");

        return new AliExpressPage<AliExpressOrder>(
            items.Select(ReadOrder).Where(order => order.SubOrderId.Length > 0).ToArray(),
            GetInt(result, "current_page_no"),
            GetInt(result, "total_page_no"),
            GetInt(result, "total_record_count"),
            GetString(result, "min_query_index_id"),
            GetString(result, "max_query_index_id"));
    }

    public static JsonElement GetResponseEnvelope(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return root;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name == "error_response" ||
                property.Name.EndsWith("_response", StringComparison.Ordinal))
            {
                return property.Value;
            }
        }

        return root;
    }

    private static AliExpressProduct ReadProduct(JsonElement item) =>
        new(
            GetString(item, "product_id") ?? string.Empty,
            GetString(item, "sku_id"),
            GetString(item, "product_title") ?? string.Empty,
            GetString(item, "product_main_image_url"),
            GetString(item, "product_detail_url"),
            GetString(item, "promotion_link"),
            GetString(item, "target_sale_price"),
            GetString(item, "target_original_price"),
            GetString(item, "target_sale_price_currency"),
            GetString(item, "commission_rate"),
            GetString(item, "hot_product_commission_rate"),
            GetString(item, "discount"),
            GetString(item, "evaluate_rate"),
            GetLong(item, "lastest_volume"),
            GetString(item, "first_level_category_id"),
            GetString(item, "first_level_category_name"),
            GetString(item, "second_level_category_id"),
            GetString(item, "second_level_category_name"),
            GetString(item, "shop_id"),
            GetString(item, "shop_name"),
            GetString(item, "shop_url"),
            GetString(item, "tax_rate"),
            GetStringArray(item, "product_small_image_urls"),
            GetString(item, "product_video_url"),
            GetString(item, "ean_code"));

    private static AliExpressOrder ReadOrder(JsonElement item) =>
        new(
            GetString(item, "sub_order_id") ?? string.Empty,
            GetString(item, "order_id") ?? GetString(item, "parent_order_number"),
            GetString(item, "order_status"),
            GetString(item, "product_id"),
            GetString(item, "product_title"),
            GetString(item, "tracking_id"),
            GetString(item, "custom_parameters") ?? GetString(item, "customer_parameters"),
            GetString(item, "commission_rate"),
            GetDecimal(item, "estimated_paid_commission"),
            GetDecimal(item, "estimated_finished_commission"),
            GetDecimal(item, "paid_amount"),
            GetDecimal(item, "finished_amount"),
            GetString(item, "settled_currency"),
            GetString(item, "paid_time"),
            GetString(item, "finished_time"),
            GetString(item, "completed_settlement_time"),
            GetString(item, "ship_to_country"),
            GetYNBoolean(item, "is_affiliate_product"),
            GetYNBoolean(item, "is_hot_product"),
            item.GetRawText(),
            GetString(item, "incentive_commission_rate"),
            GetDecimal(item, "estimated_incentive_paid_commission"),
            GetDecimal(item, "new_buyer_bonus_commission"),
            GetYNBoolean(item, "is_new_buyer"),
            GetString(item, "order_platform"),
            GetString(item, "order_type"));

    private static JsonElement GetResult(JsonElement root)
    {
        var envelope = GetResponseEnvelope(root);
        if (envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("resp_result", out var responseResult) &&
            responseResult.ValueKind == JsonValueKind.Object &&
            responseResult.TryGetProperty("result", out var result))
        {
            return result;
        }

        if (envelope.ValueKind == JsonValueKind.Object && envelope.TryGetProperty("result", out var directResult))
        {
            return directResult;
        }

        return envelope;
    }

    private static IReadOnlyList<JsonElement> GetNestedArray(
        JsonElement parent,
        string containerName,
        string nestedArrayName)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(containerName, out var container))
        {
            return [];
        }

        if (container.ValueKind == JsonValueKind.Array)
        {
            return container.EnumerateArray().ToArray();
        }

        if (container.ValueKind == JsonValueKind.Object &&
            container.TryGetProperty(nestedArrayName, out var nested) &&
            nested.ValueKind == JsonValueKind.Array)
        {
            return nested.EnumerateArray().ToArray();
        }

        return [];
    }

    private static string? GetString(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            return [];
        }

        var items = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray(),
            JsonValueKind.Object when value.TryGetProperty("string", out var strings) && strings.ValueKind == JsonValueKind.Array => strings.EnumerateArray(),
            _ => default
        };

        return items
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static int? GetInt(JsonElement parent, string name) =>
        int.TryParse(GetString(parent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static long? GetLong(JsonElement parent, string name) =>
        long.TryParse(GetString(parent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static decimal? GetDecimal(JsonElement parent, string name) =>
        decimal.TryParse(GetString(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool? GetYNBoolean(JsonElement parent, string name) => GetString(parent, name) switch
    {
        "Y" => true,
        "N" => false,
        _ => null
    };
}
