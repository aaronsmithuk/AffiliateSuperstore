# Affiliate Programme implementation notes

These are project conclusions drawn from the captured AliExpress evidence. They
are not AliExpress source material and should be updated when programme rules or
account settings change.

## Tracking design

- AliExpress permits only 50 Tracking IDs per account, and an ID cannot be
  edited or removed after creation. Do not create one per product, page or
  short-lived campaign.
- Keep the number of Tracking IDs small and stable. Use them for durable channel
  boundaries only.
- Use `cn` for the internal shop or campaign, `cv` for the placement/creative,
  and `dp` for an opaque unique outbound-click ID.
- Never put an email address, account identifier or other personal data in
  `cn`, `cv` or `dp`.
- Store the `dp` value with the outbound click. Configure AliExpress S2S to
  return it as `clickid`, providing the join between a click and a paid order.

## Conversion and reporting pipeline

- Use S2S for near-real-time notification after payment.
- Treat an S2S notification as an estimated conversion, not settled revenue.
- Reconcile through the order API until each sub-order reaches Completed
  Settlement or Invalid.
- Live Order Tracking retains only 180 days. Import or export it monthly and
  keep the project's own immutable reconciliation history.
- AliExpress Traffic Report data has a two-day delay. The admin dashboard must
  label delayed and real-time figures rather than presenting them as equivalent.
- The Portal's Add to Cart report is reference-only and is not a settlement
  basis. It does not provide a basket-transfer API; the local shopping-list
  approach remains unchanged.

## Catalogue and commission operations

- API product pricing is described as real time, but locally cached catalogue
  pages still need freshness timestamps and scheduled refreshes.
- Check the Specific Product List monthly. It is published by the 10th, becomes
  effective three days later and can override normal category commission.
- Store-specific rates can also override category commission. Preserve the
  commission rate returned with each product and the rate observed on each
  attributed order.
- Prefer affiliate and Hot Products. Do not assume a non-affiliate product will
  earn commission under the account's current channel model.
- Link generation and product ingestion should record when a promotion link was
  generated so links can be refreshed before AliExpress's documented expiry
  conditions are reached.

## Admin backlog informed by the Help Centre

1. Tracking taxonomy and remaining-ID counter before allowing new IDs.
2. Outbound click log using an opaque `dp` click ID.
3. S2S endpoint, event inbox and idempotent processing.
4. Order reconciliation with paid, completed, settled and invalid states.
5. Traffic/report freshness labels and a monthly retention job.
6. Commission-rule and specific-product-list review reminders.
7. Link-age and no-click expiry monitoring.
8. Withdrawal checklist warning that the USD 15 fee can be charged even when a
   transfer fails.

## Outstanding evidence

The consolidated Help Centre reference identifies a new Affiliate Programme
Service Agreement effective 1 April 2025. The full captured agreement in this
project is the 2022 version. Obtain the complete 2025 agreement, preserve it as
a new source and compare it before finalising compliance, privacy or contractual
requirements.
