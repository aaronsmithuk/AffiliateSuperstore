# Umbrella brand and domain decision

## Decision

**Wonder Aisle** is the adopted umbrella brand and **`wonderaisle.co.uk`** is
the canonical domain selected by the owner on 31 August 2026. Registration is
still a separate external action and has not been performed.

It is the strongest fit because it is short, pronounceable, broad enough for
many departments and communicates discovery without promising a combined
checkout. It also avoids AliExpress names and abbreviations. The architecture
would be:

```text
wonderaisle.co.uk/                 umbrella discovery page
wonderaisle.co.uk/plushies         The Plushy Shop
wonderaisle.co.uk/collectables     future shop identity
wonderaisle.co.uk/witchcraft       future shop identity, if commercially suitable
```

Each path remains a separately themed shop backed by the same application and
database. Use one canonical host for the MVP rather than subdomains: this
concentrates search authority, simplifies cookies and deployment, and makes
cross-department navigation natural. Any additional domains should issue a
permanent redirect to the corresponding canonical path instead of serving
duplicate pages.

Keep affiliate attribution per shop. `/plushies` should continue to use the
listed `theplushyshop` Tracking ID. Future paths should receive their own
approved Tracking ID where AliExpress permits it; otherwise use the configured
fallback Tracking ID plus the shop/campaign/click attribution already stored by
the application.

## Point-in-time checks

At 31 August 2026, Nominet RDAP returned `404` (no registered-domain record) for
all five candidates. SmarterASP's signed-in domain search subsequently showed
`wonderaisle.co.uk` as available; this was inspected without adding it to the
basket or starting registration.

1. `wonderaisle.co.uk`
2. `playfulfinds.co.uk`
3. `wonderbasket.co.uk`
4. `joyaisle.co.uk`
5. `treasuretrolley.co.uk`

The Companies House exact-name search returned no company result for the five
spaced names. General web searches found no obvious exact-name retail conflict
for Wonder Aisle. By contrast, “playful finds” is already common retail copy,
making it less ownable and less searchable as an umbrella brand.

These are discovery checks, not reservations or legal clearance. Availability
can change at any moment. Immediately before purchase:

1. rerun Nominet/registrar availability;
2. search the UK IPO register for identical and similar marks in the relevant
   retail, advertising and online-service classes;
3. check Companies House and general web/social handles again; and
4. register both `.co.uk` and `.uk` if affordable, using `.co.uk` as canonical.

No domain has been registered by this project work. The source configuration
uses the selected canonical HTTPS origin while production indexing remains
disabled, so the name can be exercised safely before launch.

Official lookup sources:

- [Nominet domain lookup](https://nominet.uk/lookup/)
- [UK IPO trade-mark search guidance](https://www.gov.uk/search-for-trademark)
- [Companies House company search](https://find-and-update.company-information.service.gov.uk/)
