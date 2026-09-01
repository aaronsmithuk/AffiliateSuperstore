# Webmaster and consent-based analytics setup

Status: Google Analytics property and web stream created; Search Console domain
ownership verified and sitemap accepted; Bing Webmaster signed in and awaiting
Search Console import.

Updated: 1 September 2026

## Google Analytics

- Property: `Wonder Aisle`
- Property ID: `552287228`
- Web stream: `Wonder Aisle Web`
- Stream ID: `15538576426`
- Measurement ID: `G-8XTS93L155`
- Site: `https://wonderaisle.co.uk`
- Reporting: United Kingdom, GBP, Shopping, small business
- Objectives: traffic understanding and engagement/retention
- Advertising storage, advertising user data, advertising personalisation and
  Google Signals are disabled by the site integration.

The measurement ID is not a secret. It is intentionally present in public page
markup once the visitor accepts analytics. Do not create or commit a Google
Measurement Protocol API secret; this integration does not need one.

## Consent implementation

Wonder Aisle uses Google basic consent mode:

1. No Google script or request is loaded before a visitor chooses.
2. Accept and Reject are both present in the initial notice.
3. Reject leaves the public shop fully functional.
4. Accept loads only the configured GA4 tag, with every advertising consent
   type denied.
5. The preference and GA cookies are limited to six months and the GA expiry is
   not refreshed on every visit.
6. A persistent footer control reopens the choices. Withdrawing consent removes
   accessible GA cookies and reloads without the Google tag.
7. The privacy and cookie notice documents the data, purpose, recipient,
   lawful basis and retention.

The existing first-party catalogue impression totals remain separate. They do
not store an IP address, user agent, identifier, fingerprint or visitor record.

## Google Search Console

Use one domain property for `wonderaisle.co.uk`, covering HTTPS/HTTP and all
subdomains. Ownership is verified with a DNS TXT record. The TXT value belongs
in the DNS provider only and must not be committed to this repository.

After verification:

1. submit `https://wonderaisle.co.uk/sitemap.xml`;
2. inspect `/`, `/plushies` and one quality-gated product URL;
3. link the Search Console property to the Wonder Aisle GA4 web stream;
4. use Search Console query/page exports as an optional first-party editorial
   demand input, never as an automatic publishing instruction.

## Bing Webmaster Tools

The Bing account uses the same Google identity as Search Console. After the
Search Console domain property is verified, use Bing's GSC import. This imports
the verified site and sitemap without a second public verification token.

Then run one site scan, confirm the canonical HTTPS origin, and leave IndexNow
or URL Submission API work until catalogue publication volume justifies another
credential and integration.

## Verification checklist

- [x] GA4 property created.
- [x] GA4 web stream created.
- [x] Measurement ID stored as non-secret application configuration.
- [x] Basic consent banner implemented.
- [x] Initial and rejected states verified locally with no Google script.
- [x] Privacy and cookie notice updated.
- [ ] GA4 property placed in the Hydra Analytics account.
- [x] Search Console DNS TXT record published and verified.
- [x] Sitemap submitted to Search Console (13 discovered URLs on submission).
- [ ] Search Console linked to GA4.
- [ ] Site imported into Bing Webmaster Tools.
- [x] Consent release deployed and checked over canonical HTTPS.
