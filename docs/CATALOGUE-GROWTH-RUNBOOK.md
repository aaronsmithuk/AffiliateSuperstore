# Catalogue growth runbook

This runbook accelerates the live plushies catalogue without weakening product
safety, duplicate, editorial, affiliate-link or indexing gates. It separates
candidate throughput from public publication so a larger working queue cannot
silently create a larger public risk.

## Current baseline and targets

- Public catalogue: 19 approved active products.
- Collections: eight configured, with `animal-friends` and
  `weird-wonderful` currently published.
- First commercial target: 50 approved active products distributed across
  useful collections.
- Second target: 100 approved active products after the first target has shown
  stable freshness, click and quality behaviour.
- Automatic publication remains limited by readiness 1.00, duplicate holds
  from confidence 0.75, the automatic safety circuit and the daily publication
  cap stored in the shop policy.

## Safe acceleration order

1. Grow the candidate pool through standard search and Smart Match while
   keeping API pacing, bounded pages and provider errors visible.
2. Assign relevant existing and newly discovered candidates to draft
   collections in bounded, reviewable batches. Assignment never approves a
   product or publishes a collection.
3. Prepare AI editorial drafts for suitable products assigned to either
   published or draft collections. This expands the human-review queue, not
   autonomous publication eligibility.
4. Keep autonomous preparation restricted to products assigned to a published
   collection. Keep all readiness, duplicate, availability, price, link,
   provenance and source-change checks unchanged.
5. Prefer raising candidates considered per automatic run before raising the
   number that may publish per day. More consideration prevents held candidates
   from wasting capacity without increasing the daily public-change ceiling.
6. Publish a draft collection only through the explicit owner action after it
   reaches its configured minimum of indexable products and passes the existing
   SEO assessment.

## Initial accelerated operating profile

The first accelerated profile should use:

- hourly automatic review;
- five candidates considered per run;
- no more than two automatic publications per UTC day;
- minimum readiness 1.00;
- duplicate hold confidence 0.75;
- daily product-copy AI allowance USD 0.25;
- shared monthly AI ceiling USD 5.00.

Only the candidates-per-run setting changes from the current restricted pilot.
The owner must review the live scorecard and save that change explicitly. The
system must not apply this profile from configuration or a deployment.

## Collection expansion loop

For each draft collection:

1. Review its generic, brand-safe discovery queries.
2. Run bounded collection discovery and inspect API failures or rejected items.
3. Review suggested matches and batch-assign only the visibly selected rows.
4. Prepare or review product editorial copy through the normal approval queue.
5. Confirm at least the configured minimum number of products are active,
   approved, linked, image-complete, priced, fresh and indexable.
6. Review the collection title, introduction, SEO metadata and product fit.
7. Publish through the explicit collection control and verify its canonical,
   sitemap entry, ItemList structured data and public product count.

Suggested order after the two live collections is `ocean-friends`,
`cute-food`, `plush-cushions`, `fantasy-friends`, `mini-plush`, then
`gamer-favourites`. Actual readiness, not this ordering, decides publication.

## Daily evidence check

During acceleration, review:

- public product total and change since the previous UTC day;
- candidates prepared, held and published;
- hold reason distribution, especially probable duplicates and source changes;
- AI successes, failures, budget blocks and cost;
- queue delay, retries, expired leases and dead letters;
- unavailable products and affiliate-link failures;
- products assigned to no collection;
- each draft collection's assigned, approved and indexable counts;
- any automatic safety downgrade to Shadow mode.

Stop automatic publication and investigate if a wrong product publishes, a
confirmed/probable duplicate passes, unsupported copy reaches public view, the
same failure repeats three times, a dead letter appears, or the safety circuit
downgrades the policy. Candidate discovery and draft preparation may continue
only when the incident does not compromise their evidence.

## Later cap review

Do not raise the daily publication cap merely because the queue is large. A
higher cap remains an owner decision supported by the seven-day scorecard:
enough automatic decisions and publications, a drained queue, and zero AI
failures, budget blocks, cancelled reviews, dead letters or safety pauses.
Every increase should be one bounded step followed by another observation
window.
