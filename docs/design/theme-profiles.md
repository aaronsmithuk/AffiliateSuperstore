# Theme profiles and semantic-token proposal

Status: proposal for review; no application or CSS changes are included

## Contract fit

The existing shop contract is sufficient for this exploration:

| Shop setting | Current CSS input | Proposed responsibility |
| --- | --- | --- |
| `Theme.Profile` | `data-theme` | Select one documented profile recipe |
| `Theme.PrimaryColour` | `--theme-brand` | Links, primary actions and strong identity areas |
| `Theme.AccentColour` | `--theme-accent` | Small highlights and subtle fills; never assumed to support text |
| `Theme.CanvasColour` | `--theme-canvas` | Shop-page background |
| `Theme.SurfaceColour` | `--theme-surface` | Cards, forms, sheets and notices |
| `Theme.TextColour` | `--theme-text` | Primary foreground |
| `Theme.LogoText` | rendered shop heading | Accessible textual identity; an image mark remains optional |

No arbitrary shop CSS or additional database fields are required to express the
three concepts. A profile should convert the five configured colours into a
controlled semantic recipe, validate the resulting contrast, and select only
approved typography, radii, texture and image treatment.

The five configured values should continue to be the authoring surface. Values
such as muted text, borders and subtle fills should be profile defaults or
derived tokens, not editable shop settings. This prevents a configuration from
creating dozens of untested colour combinations.

## Shared primitives

These remain identical across every public shop and the admin unless a separate
admin density value is explicitly documented.

| Primitive | Proposal |
| --- | --- |
| Spacing | `4, 8, 12, 16, 24, 32, 48, 64` px |
| Minimum touch target | `44 × 44` px |
| Control height | `44` px compact, `52` px prominent |
| Content widths | `1280` px storefront, `720` px reading measure |
| Breakpoints | `480`, `768`, `1024`, `1280` px |
| Image ratios | card `1:1`; lead story `4:3`; product gallery `1:1` |
| Motion | `120 ms` feedback, `180 ms` disclosure/sheet, no decorative loops |
| Focus ring | `2 px` surface gap plus `3 px` focus colour; never clipped |
| Underline | present for in-body links; optional only where another cue is explicit |

At `prefers-reduced-motion: reduce`, movement becomes an immediate state change,
skeleton shimmer is removed and carousels never auto-advance.

## Shared semantic roles

The shared component layer should consume roles, not profile colour names.
Names below are proposals, not edits to `tokens.css`.

| Role | Meaning |
| --- | --- |
| `color-brand` | Primary identity, links and primary action fill |
| `color-on-brand` | Foreground on brand; normally white after contrast validation |
| `color-accent` | Decorative or selected-state accent |
| `color-canvas` | Page background |
| `color-surface` | Raised/default content surface |
| `color-surface-subtle` | Low-emphasis panel, notice or skeleton surface |
| `color-text` | Primary foreground |
| `color-muted` | Secondary text that still meets AA for its size |
| `color-line` | Borders, dividers and control boundaries |
| `color-focus` | Keyboard focus and selected outline |
| `color-success` | Available/saved confirmation when paired with text or icon |
| `color-danger` | Error and destructive confirmation, never ordinary emphasis |
| `color-unavailable` | Unavailable label and disabled-state foreground |
| `shadow-card` | Card elevation, allowed to be none in flatter profiles |
| `radius-control/card/sheet` | Finite profile shape recipe |
| `font-display/body/meta` | Approved local type roles |

Component-level aliases should then describe intent: `button-primary-fill`,
`button-primary-text`, `card-border`, `notice-partner-fill`,
`product-price-text`, `saved-indicator`, `skeleton-fill` and
`unavailable-label`. Those aliases make state testing possible without coupling
a component to `brand` or `accent` directly.

The proposed primary text, muted text, brand-button and focus colours were
spot-checked against their intended light surfaces. Ratios range from 5.47:1 to
15.61:1 for the tested foreground pairs. This is a useful baseline, not a
substitute for automated checks across every derived token and configured shop.

## Profile recipes

### `playful-curator`

This is the specialist-shop expression seen in the Playful Curator boards.

| Role | Value |
| --- | --- |
| `brand` | `#6D2E5B` |
| `on-brand` | `#FFFFFF` |
| `accent` | `#F4B860` |
| `canvas` | `#FFF8EE` |
| `surface` | `#FFFFFF` |
| `surface-subtle` | `#F8EFE8` |
| `text` | `#2B1830` |
| `muted` | `#655966` |
| `line` | `#DCCFD7` |
| `focus` | `#08757B` |
| `success` | `#1F6A52` |
| `danger` | `#A23A4A` |
| `unavailable` | `#6E676B` |
| `shadow-card` | `0 8px 24px rgb(43 24 48 / 10%)` |
| `radius-control/card/sheet` | `12 / 18 / 24` px |
| `font-display/body/meta` | `Nunito Sans / Nunito Sans / Nunito Sans` |

Profile assets: up to eight category mini-illustrations with consistent stroke
and fill treatment; one subtle paper grain below 20 KB after optimisation; one
optional approved wordmark. Product photography uses warm neutral environments,
soft natural light and uncluttered square crops.

Resolved component aliases for the recommended plushies treatment:

| Component alias | Value / role |
| --- | --- |
| Primary button | `#6D2E5B` fill, `#FFFFFF` text, `#552047` hover |
| Secondary button | `#FFFFFF` fill, `#6D2E5B` text and border |
| Card | `#FFFFFF` fill, `#DCCFD7` border, 18 px radius |
| Partner notice | `#FFF3E1` fill, `#D8C6B2` border, `#2B1830` text |
| Selected chip | `#F8E8EF` fill, `#6D2E5B` border and text |
| Focused control | 2 px surface gap plus 3 px `#08757B` ring |
| Saved indicator | `#1F6A52` state plus filled icon and visible label |
| Skeleton | `#EFE9E6` fill on surface; no shimmer under reduced motion |
| Unavailable label | `#6E676B` text on `#F1EEEC`; explicit label required |

The apricot accent remains decorative. It is not a primary button fill, body
text colour or sole indicator of a selected/saved state.

Proposed plushies configuration:

```json
{
  "Profile": "playful-curator",
  "PrimaryColour": "#6D2E5B",
  "AccentColour": "#F4B860",
  "CanvasColour": "#FFF8EE",
  "SurfaceColour": "#FFFFFF",
  "TextColour": "#2B1830",
  "LogoText": "The Plushy Shop"
}
```

### `modern-department-store`

This is the recommended shared foundation and safest default profile.

| Role | Value |
| --- | --- |
| `brand` | `#123C35` |
| `on-brand` | `#FFFFFF` |
| `accent` | `#B47A16` |
| `canvas` | `#F4F2EC` |
| `surface` | `#FFFFFF` |
| `surface-subtle` | `#EEF2F0` |
| `text` | `#18201D` |
| `muted` | `#5E6663` |
| `line` | `#D4D9D6` |
| `focus` | `#2369A7` |
| `success` | `#1D6756` |
| `danger` | `#9A3442` |
| `unavailable` | `#676D6A` |
| `shadow-card` | `0 4px 14px rgb(24 32 29 / 7%)` |
| `radius-control/card/sheet` | `8 / 10 / 16` px |
| `font-display/body/meta` | `Newsreader / Source Sans 3 / Source Sans 3` |

Profile assets: optional shop wordmark only. No background texture or category
illustration is required. Product photography uses colour-accurate studio or
quiet lifestyle crops and a consistent warm-grey background.

Proposed plushies configuration:

```json
{
  "Profile": "modern-department-store",
  "PrimaryColour": "#123C35",
  "AccentColour": "#B47A16",
  "CanvasColour": "#F4F2EC",
  "SurfaceColour": "#FFFFFF",
  "TextColour": "#18201D",
  "LogoText": "The Plushy Shop"
}
```

The umbrella brand may use this profile's primitives without inheriting a shop's
configured colours. That prevents a specialist theme from recolouring global
navigation or the admin.

### `editorial-treasure-hunt`

This profile is reserved for shops with genuine editorial content capacity.

| Role | Value |
| --- | --- |
| `brand` | `#143A70` |
| `on-brand` | `#FFFFFF` |
| `accent` | `#D4A017` |
| `canvas` | `#F3EBDD` |
| `surface` | `#FFFDF7` |
| `surface-subtle` | `#EDE2CF` |
| `text` | `#211D1A` |
| `muted` | `#655D55` |
| `line` | `#C9BBA7` |
| `focus` | `#175FB4` |
| `success` | `#35634B` |
| `danger` | `#7A2232` |
| `unavailable` | `#6F675F` |
| `shadow-card` | `0 5px 18px rgb(33 29 26 / 9%)` |
| `radius-control/card/sheet` | `2 / 4 / 8` px |
| `font-display/body/meta` | `Literata / IBM Plex Sans / IBM Plex Sans Condensed` |

Profile assets: one seamless low-contrast paper texture, up to six botanical or
index motifs, one approved stamp style and optional wordmark. Decorative assets
must be CSS backgrounds or presentational images with empty alternative text;
they never carry unique content. Product photography uses natural or collected
settings with consistent, quiet colour grading.

Proposed plushies configuration:

```json
{
  "Profile": "editorial-treasure-hunt",
  "PrimaryColour": "#143A70",
  "AccentColour": "#D4A017",
  "CanvasColour": "#F3EBDD",
  "SurfaceColour": "#FFFDF7",
  "TextColour": "#211D1A",
  "LogoText": "The Plushy Shop"
}
```

## State recipes

These rules are invariant across profiles.

| State | Required semantic treatment |
| --- | --- |
| Hover | Optional elevation or border change plus an unchanged readable label |
| Keyboard focus | Two-layer focus ring using `color-focus`; never only shadow/elevation |
| Saved | `color-success` or profile emphasis plus filled icon and `Saved` text |
| Loading | `surface-subtle` skeletons matching final geometry; live-region summary |
| Empty | Neutral status panel, literal explanation and one recovery action |
| Unavailable | Label in text, disabled partner action, retained Remove/Find similar action |
| Error | `color-danger`, icon and explanation; preserve user query and saved data |
| Disabled | Muted foreground and surface with sufficient boundary contrast; no tooltip-only reason |

## Partner hand-off copy model

Short form beside the first outbound action:

> You’ll leave this shop to check current price and availability at AliExpress.

Catalogue notice:

> Checkout, payment, delivery and returns are handled by AliExpress. We may earn
> a commission from qualifying purchases at no extra cost to you.

Product-detail expansion:

> Final price, variants, delivery estimate, seller terms and checkout are
> confirmed at AliExpress. Affiliate Superstore is not the seller and does not
> receive your payment details.

The link label should describe destination and purpose, for example `Check price
& availability at AliExpress`, rather than the generic `View` or a platform logo.

## Validation gate before implementation

- Test every configurable colour set for WCAG 2.2 AA text, control boundary and
  focus appearance requirements; reject or replace unsafe configured values.
- Test catalogue, product and saved-list pages at 320, 390, 768, 1024 and 1440
  CSS pixels, at 200% text zoom and with long imported titles.
- Verify all states without colour and with forced colours.
- Confirm the font licences, subset locally hosted WOFF2 files and prevent
  invisible text during load.
- Optimise textures and decorative assets; the system must remain complete when
  they fail to load.
- Keep the admin on its existing neutral operational language. Only shared
  accessibility fixes should cross from the storefront system into admin.
