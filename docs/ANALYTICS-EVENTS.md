# Consent-gated analytics events

Updated: 1 September 2026

Wonder Aisle uses GA4 only after a visitor accepts optional analytics. The
first-party SQL impression, outbound-click and order records remain the
authoritative commercial measurements. GA4 supplies consented navigation and
funnel context; it is not used for affiliate settlement.

## Privacy boundary

- The Google tag and every GA4 event remain blocked until consent is accepted.
- GA4 receives the canonical page location without its query string.
- Typed catalogue search phrases are never sent to GA4.
- Product titles, seller names, click IDs, basket-cookie values and customer or
  administrator identifiers are not included in event parameters.
- Product references, shop slug, placement, category, price/currency, result
  totals and boolean filter usage may be sent after consent.
- Advertising storage, advertising user data, advertising personalisation and
  Google Signals remain disabled.
- Withdrawing consent clears accessible GA cookies and reloads without the tag.

## Event catalogue

| Event | When | Important parameters |
|---|---|---|
| `view_item_list` | A shop catalogue is viewed | shop, list, result count, filter-use booleans, sort and up to 50 product items |
| `select_item` | A product is opened from a catalogue or related-products list | product reference, shop, placement and available price/category |
| `view_item` | A product-detail page is viewed | product reference, shop, category, price and currency |
| `add_to_wishlist` | A product is saved | product reference, shop, placement and available price/category |
| `remove_from_wishlist` | One saved product is removed | product reference, shop, placement and available price |
| `view_saved_list` | The anonymous saved list is viewed | shop, item count and up to 50 product items |
| `clear_saved_list` | The saved list is cleared | shop and item count |
| `catalogue_search_submit` | The search form is submitted | shop and whether non-empty text was supplied; never the text itself |
| `catalogue_filter_submit` | Catalogue filters are applied | shop, filter-use booleans and sort |
| `affiliate_handoff` | A visitor asks to leave for an AliExpress listing | product reference, shop, placement, available price/category and beacon transport hint |

`affiliate_handoff` is the primary GA4 funnel success event and should be
marked as a GA4 key event. It still represents an outbound hand-off, not a sale.
AliExpress order reconciliation is the only evidence of a qualifying order or
commission.

## Verification

For a rendered catalogue, product and saved-list page:

1. With no consent choice, confirm no Google script or request is present.
2. Accept analytics and confirm one query-free `page_view` plus the appropriate
   page event.
3. Exercise each visible interaction and inspect its event name and parameters.
4. Confirm search text, product titles, seller names and click IDs are absent.
5. Withdraw consent and confirm the tag disappears after reload.
6. Compare GA4 `affiliate_handoff` trends with the first-party outbound-click
   dashboard; expected differences include rejected consent and blocked tags.
