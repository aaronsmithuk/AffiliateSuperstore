# Conversion operations runbook

Last reviewed: 2 September 2026

This runbook controls the production path from an outbound affiliate click to
an AliExpress paid-order notification and, later, signed API settlement. S2S is
an early estimate only. `Completed Settlement` from the signed order API is the
revenue authority; `Invalid` removes an order from commission totals.

Do not use an owner, employee or related-party purchase as a live test. The
captured programme rules prohibit self-purchases. A true end-to-end result
therefore requires a legitimate unrelated customer order or written AliExpress
support assistance.

## Current activation decision

Production S2S is **not ready to enable**. Code, HTTPS, admin authentication,
the immutable inbox and reconciliation path are ready, but these external
items remain open:

1. Recheck the signed-in Commission Rules page after the verified-site
   classification has finished updating and record the displayed channel
   model and rates.
2. Obtain the complete Affiliate Programme Service Agreement effective
   1 April 2025 and compare it with the captured 2022 agreement.
3. Obtain written confirmation of the application's affiliate API quota,
   permitted cache periods and the supported successor or lifetime for the
   public API documentation currently labelled deprecated.
4. Confirm that **Portals > Tools > S2S Setting** is available for this account
   and still offers every mapping in `S2S-SETUP.md`.
5. Observe one legitimate paid event and its later signed-API settlement. An
   empty 180-day account scan is a successful API health check, not conversion
   proof.

Record the evidence using
[`aliexpress/affiliate-program/EVIDENCE-STATUS.md`](aliexpress/affiliate-program/EVIDENCE-STATUS.md).

## Pre-deployment checks

- The release contains the S2S configuration validation and endpoint tests.
- All database migrations are applied; no new migration is required for this
  runbook.
- `https://wonderaisle.co.uk` and the callback route use valid HTTPS.
- `/admin/orders` is available only to the Administrator role.
- A signed order-API smoke request succeeds. Platform code 405 is benign only
  when the response text says the result is empty.
- The latest full reconciliation is successful, covers 180 days, and is no
  older than 30 days. The incremental schedule overlaps by 48 hours.
- The normal deployment and neighbouring-site checks in
  [`PRODUCTION-RELEASE.md`](PRODUCTION-RELEASE.md) pass.

Stop if any check fails. Keep S2S disabled; reconciliation can remain enabled
because it is independently authenticated with the AliExpress API signature.

## Protected configuration

Generate at least 32 random characters in a password manager or secrets tool.
Prefer 32 random bytes encoded as base64url (43 characters). Do not reuse an
API secret, admin password or catalogue wake token.

Store only in protected hosting configuration:

```text
OrderReconciliation__Enabled=true
AliExpressS2s__VerificationToken=<32-to-512-character-random-secret>
AliExpressS2s__Enabled=false
```

Restart and verify that the normal site and signed reconciliation still work.
The application deliberately returns these states:

| State | Expected response |
|---|---|
| S2S disabled | `404 Not Found` |
| Enabled with missing, short or invalid configuration | `503 Service Unavailable` |
| Enabled with an absent or wrong fixed token | `401 Unauthorized` |
| Authorised but missing `order_id` | `400 Bad Request` |
| Authorised new event | `200 OK`, body `ok` |
| Exact repeated delivery | `200 OK`, body `ok`, no new event or order |

All callback responses carry `Cache-Control: no-store`. The fixed parameter can
still appear in reverse-proxy or Portals logs, so restrict those logs and rotate
the token after any suspected exposure.

## Controlled endpoint validation

This validates authentication, mapping and duplicate suppression; it does not
prove AliExpress delivery or commission eligibility.

1. Choose a unique synthetic sub-order ID prefixed `WA-S2S-CANARY-` and record
   it in the change ticket.
2. Set `AliExpressS2s__Enabled=true` and restart. Confirm a request without the
   token returns 401 before sending any payload.
3. Send one HTTPS request containing the fixed token and harmless documented
   fields: the synthetic `order_id`, `item_id`, `effect_pay_time`, `country`,
   `order_amount`, `currency`, `commission_rate`, `commission_fee`, `clickid`
   and `tracking_id`. Do not use a real order or click ID.
4. In `/admin/orders`, confirm exactly one S2S event and one Payment Completed
   order were added. It must be unmatched because the click ID is synthetic.
5. Repeat the identical request. Confirm both counts stay unchanged.
6. Confirm the stored allow-listed payload contains neither
   `verification_token` nor unexpected parameters.
7. Remove only the two records matching the recorded synthetic sub-order ID in
   one reviewed SQL transaction. Preview both rows before deletion, require
   exactly one inbox row and one order row, and roll back on any mismatch.
8. Set `AliExpressS2s__Enabled=false`, restart, and confirm the route is 404
   until the Portals rule is ready for the controlled live stage.

Never paste the real callback URL, token, request or hosting configuration into
source control, screenshots, chat, command history or the evidence report.

## Portals activation and live validation

1. Configure the exact destination and field mappings in
   [`S2S-SETUP.md`](S2S-SETUP.md). Choose dollars, not cents.
2. Save screenshots or exports of the mapping with account identity and secrets
   redacted. Record capture time, source URL and SHA-256 in the evidence log.
3. Enable the application endpoint immediately before enabling the Portals
   rule. Avoid a long interval with only one side enabled.
4. Confirm wrong-token traffic still receives 401 and creates no rows.
5. Wait for a legitimate unrelated-customer paid event. Record the click time,
   S2S receipt time, sub-order ID, attribution outcome and currency without
   customer identity.
6. Confirm repeat push delivery does not increase the inbox count.
7. Run signed incremental reconciliation. Confirm it updates the same
   sub-order rather than inserting another row.
8. Continue reconciliation until `Completed Settlement` or `Invalid`. Record
   base estimate, incentive/bonus, settled base, currency and timestamps. Do
   not call estimated commission revenue.
9. Compare the order with the Portals Live Order Tracking report. Investigate
   any missing `dp`, currency mismatch, differing commission or unexplained
   order before accepting the activation.

## Daily and monthly operation

Daily:

- review the latest reconciliation status and retry failures;
- review unmatched S2S events and orders, invalid orders and duplicate volume;
- compare S2S receipts with signed-API discoveries;
- investigate commission or currency changes, never silently normalise them;
- check that the callback has not started returning 401, 400 or 503.

Monthly:

- complete and retain a 180-day recovery scan;
- export or archive order evidence before AliExpress's query window expires;
- capture the Commission Rules model and current category/store overrides;
- review the Specific Product List published by the 10th and effective three
  days later;
- review quota-limit responses and the process-wide 1,100 ms pacing evidence;
- rotate the callback token if logs or access controls changed.

## Rollback

1. Disable the Portals S2S rule first so AliExpress stops sending events.
2. Set `AliExpressS2s__Enabled=false` and restart; verify the route returns 404.
3. Leave order reconciliation enabled unless the signed API itself is failing.
4. Rotate the token if disclosure is possible, even though the endpoint is off.
5. Preserve all real inbox, order and job rows for audit. Do not delete or
   downgrade settled/invalid states.
6. Record the reason, last accepted event, last successful reconciliation and
   rollback verification in the change ticket.
