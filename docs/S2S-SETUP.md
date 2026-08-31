# AliExpress S2S setup

The application has an idempotent paid-order inbox at:

```text
https://<production-domain>/integrations/aliexpress/s2s
```

It is intentionally disabled by default. AliExpress does not document a
signature or other cryptographic proof for affiliate S2S callbacks, so the
endpoint requires a long random fixed parameter and treats the push as an
estimated conversion only. The signed order API remains the source of truth
for settlement and invalidation.

## Production configuration

Store these values in protected hosting configuration, never in committed
`appsettings` files:

```text
AliExpressS2s__Enabled=true
AliExpressS2s__VerificationToken=<long-random-secret>
```

Use HTTPS only. The fixed secret can appear in reverse-proxy access logs because
AliExpress delivers it as a request parameter; restrict access to those logs
and rotate the token if it is exposed.

## AliExpress Portals rule

In **Portals > Tools > S2S Setting**:

1. Set the destination to the production endpoint above.
2. Add a fixed parameter named `verification_token` with the same secret.
3. Choose **dollars**, not cents, for the order-amount unit.
4. Map the documented fields to these exact names:

| AliExpress value | Request name |
|---|---|
| `order_id` | `order_id` |
| `item_id` | `item_id` |
| `effect_pay_time` | `effect_pay_time` |
| `country` | `country` |
| `order_amount` | `order_amount` |
| `currency` | `currency` |
| `commission_rate` | `commission_rate` |
| `commission_fee` | `commission_fee` |
| `incentive_commission_rate` | `incentive_commission_rate` |
| `incentive_commission` | `incentive_commission` |
| `is_new_buyer` | `is_new_buyer` |
| `new_buyer_bonus` | `new_buyer_bonus` |
| `dp` | `clickid` |
| `tracking_id` | `tracking_id` |
| `is_affiliate_item` | `is_affiliate_item` |
| `is_hot_product` | `is_hot_product` |
| `platform` | `platform` |
| `order_type` | `order_type` |

The inbox stores only an allow-listed subset of documented order fields. It
does not store request headers, source IPs, the verification token or arbitrary
extra parameters. Repeated delivery of the same event returns `200 OK` without
creating a second inbox event or order.

## Release check

After deploying but before enabling the AliExpress rule:

1. Confirm the admin is authenticated and the endpoint is HTTPS.
2. Enable the two protected configuration values.
3. Send a synthetic callback using a non-production order ID.
4. Confirm one S2S event and one Payment Completed order appear in
   `/admin/orders`.
5. Repeat the same callback and confirm the counts do not increase.
6. Remove the synthetic records before launch.

Do not enable S2S on local development: the admin is authenticated, but there
is no stable public HTTPS callback URL and the local token is not a production
secret.
