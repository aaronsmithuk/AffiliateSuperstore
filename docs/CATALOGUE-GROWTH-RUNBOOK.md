# Catalogue growth runbook

This runbook accelerates the live plushies catalogue without weakening product
safety, duplicate, editorial, affiliate-link or indexing gates. It separates
candidate throughput from public publication so a larger working queue cannot
silently create a larger public risk. The target operating model is autonomous
day-to-day merchandising with the owner governing exceptions and anomalies.

## Current baseline and targets

- Public catalogue: 17 approved active products after the 2 September full
  relevance reassessment returned two unsafe products to review.
- Collections: eight configured, with `animal-friends` and
  `weird-wonderful` currently published.
- First commercial target: 50 approved active products distributed across
  useful collections.
- Second target: 100 approved active products after the first target has shown
  stable freshness, click and quality behaviour.
- Automatic publication remains limited by readiness 1.00, duplicate holds
  from confidence 0.75, the automatic safety circuit and the daily publication
  cap stored in the shop policy.
- `/admin/growth` is the governance view. Its daily brief reports products and
  collections published, holds, reversible retirements, AI spend/failures,
  dead letters, suppressed approved products and any anomaly requiring action.

## Safe acceleration order

1. Grow the candidate pool through standard search and Smart Match while
   keeping API pacing, bounded pages and provider errors visible.
2. Assign relevant existing and newly discovered candidates to draft
   collections in bounded batches only when deterministic semantic matching is
   strong. Assignment never approves a product or by itself publishes a
   collection.
3. Prepare AI editorial drafts for suitable products assigned to either a
   published or draft collection. Draft membership qualifies only when the
   product strongly fits that collection; published state is not a shortcut.
4. Let automatic product publication proceed only after all readiness,
   duplicate, availability, price, link, provenance, semantic-fit and
   source-change checks pass. A held product remains private.
5. In Automatic mode, retire only permanent catalogue-scope failures with a
   reversible audit reason: non-plush products, pet products/categories,
   missing plush evidence and tobacco-themed products. Ambiguous quantities,
   licensing, baby-safety, variant-price and duplicate questions remain held.
   Returning an automatically retired item to review clears the retirement
   reason and restores the normal approval workflow.
6. Prefer raising candidates considered per automatic run before raising the
   number that may publish per day. More consideration prevents held candidates
   from wasting capacity without increasing the daily public-change ceiling.
7. In Automatic mode, publish a draft collection only when it has at least 12
   currently indexable products and passes a fresh content/SEO validation at
   the publication boundary. Record the count, threshold, actor, reason and
   mode in an immutable publication event. The owner can return it to draft.

## Initial accelerated operating profile

The first accelerated profile should use:

- hourly automatic review;
- six candidates considered per run;
- no more than two automatic publications per UTC day;
- minimum readiness 1.00;
- duplicate hold confidence 0.75;
- daily product-copy AI allowance USD 0.25;
- shared monthly AI ceiling USD 5.00.

The owner approved six candidates per hourly run on 3 September 2026. The
product publication cap and either AI budget must not be raised implicitly by
configuration or deployment.

## Collection expansion loop

The six-hour collection-growth job performs this loop for one underfilled
collection at a time:

1. Select an unpublished, underfilled collection before a published one.
2. Run bounded, brand-safe API discovery and assign at most 12 strong matches.
3. Fill unused assignment capacity from existing strong, non-rejected matches.
4. Let the hourly product pipeline prepare and approve only fully gated items.
5. Recount active, approved, linked, image-complete, priced and fresh products.
6. At 12 indexable products, rerun collection validation and publish
   automatically; otherwise leave the collection private.
7. Surface API failures, publication failures and threshold-ready private
   collections in the governance brief.

Suggested order after the two live collections is `ocean-friends`,
`cute-food`, `plush-cushions`, `fantasy-friends`, `mini-plush`, then
`gamer-favourites`. Actual readiness, not this ordering, decides publication.

## Daily evidence check

The intended owner interaction is one daily review rather than queue operation.
Check:

- `/admin/growth` totals, permanent-retirement candidates and reversible audit
  count;
- public product total and change since the previous UTC day;
- candidates prepared, held and published;
- hold reason distribution, especially probable duplicates and source changes;
- AI successes, failures, budget blocks and cost;
- queue delay, retries, expired leases and dead letters;
- unavailable products and affiliate-link failures;
- products assigned to no collection;
- each draft collection's assigned, approved and indexable counts;
- any automatic safety downgrade to Shadow mode.

The daily publication limit is a deferral, not an editorial failure. Clear
candidates blocked only by `publication.daily-limit` are retried no earlier than
the next UTC day. Other repairable holds are reconsidered no sooner than 24
hours later so the worker does not repeatedly spend on unchanged evidence.

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
