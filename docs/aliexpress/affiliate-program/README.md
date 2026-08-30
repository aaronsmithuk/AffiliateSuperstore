# Affiliate Programme evidence guide

The main entry point is
[aliexpress-affiliate-rules-research.md](aliexpress-affiliate-rules-research.md).
It cites the captured evidence in `sources/` using the following hierarchy.

The `sources/` and `archive/` directories are kept in the local working copy
for audit purposes and intentionally ignored by the public Git repository.
Source filenames below therefore act as evidence identifiers in the public
copy; the original AliExpress pages must be revisited for current terms.

Project-specific consequences are maintained separately in
[IMPLEMENTATION-NOTES.md](IMPLEMENTATION-NOTES.md), so captured source material
can remain unchanged.

## 1. Binding programme documents

- [Affiliate Program Service Agreement](sources/01-affiliate-program-service-agreement.md)
- [Affiliate Program Rules and Policies](sources/05-affiliate-program-rules-and-policies.md)
- [Rules Against Promotion of Illegal Products](sources/06-helpcentre-rules-against-illegal-products.md)

Use these first for branding, domain names, prohibited conduct, self-purchases,
commission eligibility, content licensing and programme enforcement.

## 2. Current account evidence

- [Commission Rules account view](sources/07-portals-commission-rules-account-view.md)
- [Application 6102 console observations](sources/11-app-console-app-6102-observations.md)
- [Commission tables transcription](sources/10-commission-rate-tables-transcribed.md)

These describe the account as observed on 30 August 2026. They can change
without a code release and should be rechecked before financial forecasting or
production deployment.

## 3. Affiliate operations and API evidence

- [Affiliate API guidance](sources/06-helpcentre-affiliate-api-guidance.md)
- [Affiliate API reference](sources/09-openplatform-affiliate-api-reference.md)
- [Open Platform developer documentation](sources/08-openplatform-affiliate-developer-docs.md)
- [Tracking-link guidance](sources/06-helpcentre-how-to-generate-tracking-links.md)
- [Server-to-server order guidance](sources/06-helpcentre-s2s-guidance.md)
- [Reporting guidance](sources/06-helpcentre-insights-about-reports.md)
- [Product-selection guidance](sources/06-helpcentre-product-selection-instruction.md)

Use these for the API client, catalogue refresh jobs, tracking-link lifecycle,
order reconciliation and operational monitoring.

## 4. Help Centre and general policies

- [Top questions](sources/06-helpcentre-top-question.md)
- [New user guide](sources/06-helpcentre-new-user-guide.md)
- [Glossary](sources/06-helpcentre-glossary.md)
- [Consolidated Portals Help Centre reference](sources/13-portals-help-centre-reference.md)
- [Privacy Policy](sources/02-aliexpress-privacy-policy.md)
- [Cookie Notice](sources/03-aliexpress-cookie-notice.md)
- [Terms of Use](sources/04-aliexpress-terms-of-use.md)

These clarify programme operation and privacy but do not override the binding
Affiliate Programme documents.

## Still requiring confirmation

The captured official material does not state the affiliate cookie duration,
the click-attribution tie-break rule, or the exact API QPS/daily quota for this
application. Those points require written AliExpress support confirmation and
must not be inferred from Dropshipper documentation.

The account was also observed under the Non-Transparent Channels Commission
Model while its verified affiliate-store declaration was awaiting adjustment.
Recheck the live Commission Rules page before using the report's default-channel
revenue assumptions.

The consolidated Help Centre reference states that a newer Affiliate Programme
Service Agreement became effective on 1 April 2025. The full agreement currently
captured in this directory is the 31 March 2022 version. Obtain and compare the
complete 2025 agreement before treating the contractual review as complete.
