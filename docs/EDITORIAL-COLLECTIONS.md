# Editorial collections

Wonder Aisle uses first-party editorial collections instead of exposing AliExpress's source taxonomy as the primary customer navigation. A collection is scoped to one shop, has its own copy and SEO metadata, and can contain products at any editorial-review stage. Only approved, eligible products with an active affiliate link are shown publicly.

## Initial plush taxonomy

The admin can create these eight unpublished drafts with **Seed recommended drafts**:

1. Animal Friends
2. Ocean & River Friends
3. Weird & Wonderful
4. Fantasy & Prehistoric
5. Gamer Favourites
6. Cute Food & Novelty
7. Plush Cushions
8. Minis & Bag Charms

The names and discovery phrases deliberately avoid marketplace, entertainment-franchise and named-character terms. Product-level intellectual-property review still applies: a generic collection name does not make an unlicensed product suitable for publication.

## Admin workflow

1. Open `/admin/collections` and seed the recommended drafts.
2. Review and edit the collection name, introduction, SEO title, SEO description and generic discovery queries.
3. Select **Discover products** to run one first-page AliExpress search per configured phrase. Imported products are assigned to the collection as catalogue candidates, not approved or published.
4. Review product imagery, seller information, variants, intellectual-property flags and editorial copy in the catalogue admin.
5. Add or remove products manually where the search results need correction.
6. Publish the collection only when its indexable-product target is met. The default target is 12.

The collection list shows the curation funnel for every collection: assigned
candidates, editorially approved products and products that pass every indexing
gate. Selecting a collection adds the publicly eligible stage and separates the
remaining blockers into approval, catalogue/link eligibility, editorial copy,
image, current price and snapshot freshness counts.

Use the membership filters to focus on assigned, unassigned or assigned products
that still need work. Assigned products are kept visible even if they later
become inactive or ineligible so an editor can remove stale memberships. Product
search accepts either title text or any part of the AliExpress product ID.

### Suggested matches from the existing catalogue

Selecting a collection opens **Suggested matches** by default. This view does
not call AliExpress, use a model or change membership. It ranks unassigned,
active and eligible products already held in the shop catalogue, up to a bounded
2,000-product scoring pool.

The deterministic matcher:

- normalises case, simple plurals and common mini-plush terms such as
  `keyring`/`keychain`;
- ignores generic wording such as `plush`, `toy`, `soft` and `cute`;
- compares each product's source and editorial titles plus normalized identity
  title with the collection's discovery queries and short description; and
- uses the source category only as supporting evidence when a product title has
  already matched a discovery-query term.

Each row shows a relevance score and the query, scope or category terms that
contributed to it. The score is a lexical shortlist signal, not a probability or
approval decision. A score of 65 or more enters the suggested view. The operator
must still inspect the product and choose **Add**; recommendations create no
membership, approval or publication records by themselves. Editing the
collection wording changes the next ranking, so keep discovery queries specific
and generic-brand-safe. If no strong existing match is available, use **All
candidates** or run **Discover products**.

Discovery stops on an API failure and reports the completed work. Successful results found before the failure remain non-public candidates so they are not lost. Running discovery again is safe: existing product and collection memberships are reused rather than duplicated.

## Publication and SEO safeguards

A collection cannot be published until enough products pass the existing product SEO policy. A qualifying product needs approved editorial copy, an image, a current positive price, a recent snapshot, eligibility, and an active shop-specific affiliate link.

Published collection pages use `/{shop-slug}/{collection-slug}` and provide:

- canonical metadata and collection-specific title and description;
- `CollectionPage` and `ItemList` structured data;
- collection-aware analytics metadata;
- affiliate disclosure and marketplace checkout wording;
- `noindex` until the collection still meets its configured product threshold.

The sitemap independently applies the same threshold. Empty published collections do not appear in the shop navigation, and thin collections do not enter the sitemap.

## Release procedure

The `AddEditorialCollections` migration creates `Collections` and `CollectionProducts`. Apply the migration through the normal release process before using the admin page. Seeding is an explicit post-release admin action; the migration does not create or publish editorial content.

For the first live content pass, seed all eight drafts, run discovery on two or three high-intent collections, and curate at least 12 genuinely useful products in each before publication. Animal Friends, Ocean & River Friends, and Weird & Wonderful are the strongest starting set because they align with the existing broad plush catalogue without relying on protected franchises.
