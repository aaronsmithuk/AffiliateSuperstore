# Wonder Aisle design-system brief

Last updated: 30 August 2026

## Purpose

Create a distinctive, accessible visual system for a neutral affiliate
superstore that can host multiple themed specialist shops under paths such as
`/plushies`. The system must feel curated and trustworthy while being clear
that checkout, payment, delivery and returns take place on AliExpress.

This brief is intentionally separate from implementation. A visual-design task
may create mockups, mood boards and theme proposals without editing the active
Razor, Blazor or CSS files.

## Existing implementation contract

- Public storefront: ASP.NET Core Razor Pages.
- Operational admin: interactive Blazor with a deliberately separate control-
  room visual language.
- Bootstrap has been removed completely.
- Shared primitives and semantic tokens live in
  `src/AffiliateSuperstore.Web/wwwroot/css/tokens.css`.
- Public components live in
  `src/AffiliateSuperstore.Web/wwwroot/css/site.css`.
- Admin components live in
  `src/AffiliateSuperstore.Web/wwwroot/css/admin.css`.
- Shop configuration supplies a controlled theme profile plus brand, accent,
  canvas, surface and text colours. Components consume semantic tokens rather
  than reading raw shop settings.

## Design principles

1. Curated specialist shop, not a generic marketplace clone.
2. Warm and enjoyable without looking childish or untrustworthy.
3. Product imagery and useful facts lead; decoration supports comprehension.
4. Affiliate disclosure and AliExpress hand-off are clear but not alarming.
5. Shop identities can differ substantially while navigation, accessibility,
   spacing and interaction behaviour remain consistent.
6. No AliExpress branding, red/orange trade dress or identity that could imply
   the platform is AliExpress itself.
7. Mobile-first and usable at 320 CSS pixels; layouts must also work well on
   wide desktop screens.
8. WCAG 2.2 AA contrast, keyboard focus, reduced-motion support and 44-pixel
   touch targets are required.

## Theme architecture to design for

The shared system owns:

- spacing scale;
- typography roles;
- focus and interaction states;
- content widths and responsive breakpoints;
- buttons, inputs, filters, cards, notices, price treatments and breadcrumbs;
- navigation and footer structure;
- accessibility and motion rules.

Each shop may own:

- a named profile such as `playful`, `mystical` or `editorial`;
- brand, accent, canvas, surface and text colours;
- approved logo/wordmark assets;
- illustration, texture and photography treatment;
- a controlled display-typeface choice when locally hosted and licensed.

Do not propose arbitrary per-shop CSS or one-off component markup. A new shop
should be expressible through tokens, assets and one documented profile.

## Required visual exploration

Produce three meaningfully different storefront directions using the same
information architecture:

1. `Playful curator` — suitable for plushies and friendly collectables.
2. `Modern department store` — neutral enough to become the umbrella brand.
3. `Editorial treasure hunt` — discovery-led, with stronger storytelling.

For each direction show:

- desktop and mobile catalogue landing pages;
- product card states and a product detail page;
- search and filter controls;
- saved-list state;
- affiliate disclosure and outbound hand-off treatment;
- empty, loading and unavailable states;
- proposed semantic token values and type scale.

The admin does not need per-shop theming. It should remain calm, dense and
operational, with only accessibility and component-consistency refinements.

## Deliverables

- `docs/design/design-directions.md` comparing the three directions.
- PNG mockups under `docs/design/mockups/`.
- `docs/design/recommended-direction.md` with a reasoned recommendation.
- `docs/design/theme-profiles.md` mapping visual decisions to the existing
  semantic-token and shop-theme contract.
- A short list of any new reusable components or assets required.

Do not implement the winning direction until it has been reviewed. Do not edit
application source, database migrations or current CSS in the design task.
