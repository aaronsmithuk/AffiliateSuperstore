# Search and AI content governance

Status: standing product policy

Effective: 1 September 2026

Owner: the product owner is accountable for this policy and for naming any
editor or reviewer who may approve public content.

Review: check the linked Google policies every quarter and whenever Search
Console reports a manual action, a broad indexing loss, or Google announces a
relevant spam-policy change. Record material policy changes in this document
before changing automation or publication rules.

## Decision

AI-assisted content is allowed. Content made at scale primarily to obtain search
traffic, without original value for visitors, is not. The durable question is
not whether AI touched a page, how slowly it was published, or whether its prose
looks human. The question is whether the page exists to serve a real visitor,
adds value that the source feed and competing results do not, is accurate and
transparent, and has received proportionate editorial care.

This is both a Google-policy requirement and the product standard for Wonder
Aisle. Passing a mechanical validator only makes a page eligible for editorial
review; it never creates an entitlement to indexing or publication.

## Assessment of the video

The supplied video is directionally right about the principal risk, but some of
its explanations are speculation or unsafe if treated as tactics.

| Video claim | Assessment | Product rule |
|---|---|---|
| Google does not ban AI content; it acts against scaled content abuse. | Valid and directly supported by Google. The method of production is secondary to purpose, originality and value. | AI may research, structure or draft. It may not bypass evidence, validation or human publication approval. |
| Repetitive, city-swapped or otherwise near-identical pages are risky. | Valid. These can be scaled content or doorway abuse when created to capture query variations. | Do not create one URL per keyword, city, size, colour or query variation unless each URL serves a distinct user need with substantial page-specific value. |
| Lack of real experience and original detail is a warning sign. | Directionally valid. Google asks whether content demonstrates first-hand expertise and provides original information, testing or analysis. | Never invent experience. Use verified source facts, documented curation, original comparisons, first-party observations and genuine authorship. |
| Bounce-back behaviour is Google's biggest detection signal. | Not established by the cited Google policies. Google recommends Analytics engagement data for understanding visitors, but Search Console and Analytics are separate systems and Google does not document GA bounce rate as the decisive spam signal. | Use engagement and conversion as product diagnostics, never as proof of compliance or a ranking hack. Do not add friction, fake chat or unnecessary interaction to keep people on a page. |
| Publishing hundreds of pages overnight is itself a trigger; publish at a human pace. | Unsupported as a standalone rule. Scale plus low value and ranking-manipulation purpose is the policy issue. A slow release does not rehabilitate weak pages, and a large batch of genuinely useful pages is not automatically spam. | Publish only as fast as evidence, review, freshness and monitoring capacity allow. Never throttle merely to imitate a human. |
| Real About and Contact pages help. | Valid only when truthful and useful. Fabricated staff, addresses, photos, credentials or contact capabilities are deceptive. | Identify the real operator, business model, editorial method and a working contact route. Do not manufacture legitimacy signals. |
| Separate standalone microsites are safer than an interconnected network. | Misleading. Google expressly lists creating multiple sites to hide the scaled nature of content as scaled content abuse, and similar sites/pages can be doorway abuse. | Keep coherent shops under the Wonder Aisle brand and canonical domain unless a future site has a genuine independent audience, purpose, operation and editorial proposition. Never build satellites to multiply search coverage or hide scale. |
| An AI phone/chat agent will increase engagement and therefore protect rankings. | Unsupported and potentially deceptive. A useful support feature can help visitors, but it is not an SEO shield. | Add an agent only to solve a validated user need, disclose that it is automated, constrain it to supported facts, and measure task success rather than dwell time. |
| Algorithmic drops are commonly 50–80%. | Anecdotal, not a dependable policy or forecasting figure. | Do not use a percentage drop to diagnose cause. Investigate Search Console, affected URL cohorts, technical health and content quality. |

## Non-negotiable rules

1. Every indexable URL must have a named audience, a distinct user task and a
   documented reason to exist independently of its target query.
2. A merchant/API feed is source evidence, not publishable editorial value.
   Copying, lightly rewriting, synonymising or stitching feed data is not enough.
3. Wonder Aisle must add a meaningful benefit such as original selection,
   current normalization, useful comparison, clear category navigation,
   documented evaluation, or first-party analysis.
4. AI output is an untrusted suggestion. A named human remains accountable for
   every public title, description, guide, comparison and recommendation.
5. AI must never invent or infer price, stock, delivery, dimensions, pack size,
   material, safety, authenticity, licensing, ratings, popularity, testing,
   ownership or first-hand experience. Dynamic facts come from the authorized
   source projection and display with appropriate qualification and freshness.
6. Do not fake authors, biographies, credentials, business addresses, local
   knowledge, photographs, tests, reviews, calls, chats or customer activity.
7. Do not create pages or sites primarily to rank for permutations of keywords,
   locations, product attributes or questions. Consolidate overlapping intent.
8. Do not build private blog networks, satellite domains, reciprocal site rings
   or multiple brands intended to conceal common ownership or content scale.
9. Affiliate relationships must be prominent to users. Affiliate outbound links
   use `rel="sponsored"`; the existing `nofollow` value may remain alongside it.
10. Search snippets, titles, meta descriptions, image alt text and structured
    data are content too. They must be accurate, supported and consistent with
    the visible page.
11. There is no publication quota and no "human pace" exemption. Publication is
    limited by review quality, evidence freshness and the ability to maintain
    what is already indexed.
12. No page is published merely because a model generated it, a keyword tool
    suggested it, competitors have it, or a minimum word/character count passed.

## Indexability contract by page type

### Product pages

An indexable product page must:

- represent an active, approved product with a working affiliate destination;
- show current source-backed price/availability context without freezing those
  dynamic facts into editorial prose;
- contain a reviewed title and description that add shopper-relevant context
  rather than paraphrasing the merchant title;
- expose its advertising relationship and identify AliExpress/the seller as the
  transaction party;
- include useful product facts and media that the authorized source permits;
- have one canonical URL and no indexable sort/filter/query variants; and
- be withdrawn from the sitemap and indexable set when its evidence becomes
  stale, its link fails, the product becomes unavailable, or editorial approval
  is revoked.

The current length, image, price and freshness checks in `CatalogueSeoPolicy`
are necessary mechanical gates. They are not a sufficient originality or
helpfulness assessment.

### Shop and category pages

An indexable landing page must represent a useful browseable collection, not a
keyword container. It needs a coherent scope, enough approved products to make
the choice useful, meaningful categorisation or comparison, and original
introductory guidance. Search, sort, filter, pagination and empty-result URLs
remain non-indexable unless an explicit review establishes a distinct durable
user need and canonical strategy.

Adding a new shop to the existing application is preferable to launching a new
domain. A separate domain requires a written decision showing an independently
valuable audience, purpose, identity, ownership disclosure and content plan; SEO
coverage alone can never justify it.

### Guides, comparisons and reviews

An article needs a brief that records the intended audience, user task, evidence
sources, unique contribution, author/reviewer, overlap check and refresh/expiry
rule. It must contribute original analysis beyond product-feed summaries.

Use "review", "tested" or first-person experience claims only when a named
person actually performed the work and the page explains the method and evidence.
If products were evaluated from documented source data rather than handled,
describe the work as curation or comparison and say what was and was not checked.

AI-generated briefs and drafts remain review-only. During the initial editorial
pilot, the existing cap of four reviewed drafts per month remains a capacity and
learning control, not a way to appear human to Google.

### Utility, search and generated pages

A page that claims a tool or function must perform it. Do not index empty states,
fabricated answers, internal-search results, faceted permutations or pages that
merely funnel visitors to another useful page. If a generated page cannot pass
the same evidence and value review as a hand-written page, exclude it from Search
or do not create it.

## Publication workflow

Every new indexable content unit follows this sequence:

1. **Need:** record the audience, task and first-party or independently verified
   evidence that the page is needed.
2. **Overlap:** compare with existing indexed and proposed pages. Merge or extend
   an existing page when the intent substantially overlaps.
3. **Evidence packet:** capture permitted source facts, timestamps, provenance,
   known limitations and any genuine first-hand contribution.
4. **Draft:** a person or AI may draft only from that packet. Prompts must treat
   merchant text as untrusted data and prohibit unsupported claims.
5. **Mechanical validation:** run claim, freshness, link, eligibility, metadata,
   canonical, structured-data and duplication checks. A failure blocks review.
6. **Human review:** a named reviewer answers the checklist below and either
   rejects, revises or approves an immutable version. Approval cannot be inferred
   from absence of warnings.
7. **Publish:** update internal links and the sitemap only after approval. The
   public request path never waits for or calls a model.
8. **Observe:** verify rendering, destination, canonical, robots directive and
   structured data; then monitor user outcomes and Search Console by page cohort.
9. **Maintain:** refresh, merge, redirect, `noindex`, or remove content when facts,
   demand or value change. Never change a date without a substantive update.

### Human review checklist

A reviewer must be able to answer yes to every applicable question:

- Would this page still be worth publishing if search engines did not exist?
- Does it solve a defined visitor task completely enough that another search is
  not required merely because we omitted the useful part?
- Is its independent value clear when compared with the merchant page and the
  closest Wonder Aisle page?
- Are all factual and comparative claims supported by the recorded evidence?
- Is the wording honest about what we reviewed, tested or did not verify?
- Are author/operator, automation and affiliate disclosures clear where a user
  would reasonably expect them?
- Is the title descriptive rather than exaggerated, keyword-stuffed or misleading?
- Are media original or authorized, relevant, accurately described and not used
  as fake evidence of experience?
- Are the canonical, robots, sitemap and structured-data decisions correct?
- Is there a named owner and a practical refresh, expiry or withdrawal rule?

## AI use policy

Permitted uses include structuring verified facts, suggesting alternative copy,
identifying possible duplication, summarising an evidence packet for a reviewer,
and generating review-only briefs. Models and prompts must be versioned, outputs
cached by stable input hash, costs capped, and every invocation auditable as set
out in the AI automation plan.

Forbidden uses include autonomous publication, bulk keyword-page generation,
synthetic first-hand accounts, fabricated local detail, unsourced product claims,
automated date refreshing, content spinning, or using multiple sites to disguise
common generation. Making each draft semantically different is not a defence if
the pages remain unoriginal or unnecessary.

Disclose automation when it would help a reasonable visitor understand how the
content was made—especially for material generated substantially by AI. The
disclosure must be accurate and must not imply human testing or authorship that
did not occur.

## Monitoring and quality operations

Use metrics to learn whether the product helps people, not to simulate ranking
signals.

Monitor at least monthly, by page type and publication cohort:

- Search Console impressions, clicks, queries, indexing state and manual actions;
- broken affiliate destinations, stale source facts and sitemap eligibility;
- useful outbound clicks, saves and successful catalogue tasks;
- GA engagement/conversion trends where consented, treated as diagnostic rather
  than a direct ranking score;
- editorial corrections, unsupported-claim findings, rejection/acceptance rate,
  user complaints and review backlog; and
- duplicate-intent or high-similarity candidates within the site.

Do not optimize for dwell time, page count, word count or bounce rate in isolation.
A visitor who quickly obtains the right answer or follows the correct affiliate
link may have completed the task successfully.

Sample at least 10% of newly approved AI-assisted items each month, with a minimum
of five when five or more were published. Review all user-reported factual issues.
Pause new AI-assisted publication when the review backlog exceeds its agreed SLA,
an unsupported claim reaches production, or a cohort shows a repeated quality
failure. Resume only after the cause, affected scope and regression control are
documented.

## Search incident response

For a large traffic or indexing drop:

1. Stop new generated-content publication, but do not mass-delete or rewrite the
   site based on timing alone.
2. Check Search Console manual actions, security issues, Page Indexing, crawl and
   performance reports; verify robots, canonicals, sitemaps, availability and
   recent releases.
3. Segment the loss by page type, query, country, device and publication cohort.
4. Compare affected pages against unaffected pages using this policy. Look for
   thin affiliation, overlapping intent, unsupported claims, stale facts and
   weak original value.
5. Repair the user problem. Merge duplicates, improve from real evidence, or
   remove/`noindex` pages that should not exist. Do not merely rephrase them with
   another model or slow their republication.
6. If a manual action exists, correct the full affected scope, document the work
   and submit a reconsideration request through Search Console. Algorithmic
   changes do not have a reconsideration-request shortcut.
7. Record the incident, decision and preventive control in the project plan or
   this policy.

## Repository implementation map

Controls already present on 1 September 2026:

- AI suggestions are review-only and cannot set source facts;
- editorial content has immutable versions, named reviewers, claim validation,
  diffs and restore-as-new-version history;
- only approved, active, linked and sufficiently fresh products reach the public
  catalogue and quality-gated sitemap;
- canonical URLs and non-indexable filter/search states are in place;
- affiliate disclosures are visible and outbound purchase links use
  `rel="sponsored nofollow"`; and
- AI calls are provider/version audited, budget-capped and excluded from public
  request handling.

Required controls before scaling editorial or indexable page creation:

- store the audience, user task, unique-value statement, evidence packet,
  reviewer and maintenance rule with each proposed guide or landing page;
- add an overlap/cannibalisation check before an indexable URL is approved;
- treat `CatalogueSeoPolicy` as mechanical eligibility and add a separately
  recorded editorial-value approval for any new indexable page type;
- report indexed and proposed URL counts by page type alongside approval,
  rejection, stale-content and correction rates; and
- add the quarterly policy review and content sampling outcomes to the durable
  admin/audit workflow before publication volume rises materially.

## Primary sources

This policy takes its authority from current first-party Google documentation,
not from the video or any claimed detector recipe:

- [Google Search guidance on generative AI content](https://developers.google.com/search/docs/fundamentals/using-gen-ai-content)
- [Spam policies for Google web search](https://developers.google.com/search/docs/essentials/spam-policies)
- [Creating helpful, reliable, people-first content](https://developers.google.com/search/docs/fundamentals/creating-helpful-content)
- [Optimizing for generative AI features on Google Search](https://developers.google.com/search/docs/fundamentals/ai-optimization-guide)
- [Qualifying affiliate and paid outbound links](https://developers.google.com/search/docs/crawling-indexing/qualify-outbound-links)
- [Using Search Console and Google Analytics data for SEO](https://developers.google.com/search/docs/monitor-debug/google-analytics-search-console)
- [Debugging drops in Google Search traffic](https://developers.google.com/search/docs/monitor-debug/debugging-search-traffic-drops)
- [Search Console manual actions report](https://support.google.com/webmasters/answer/9044175)

The [video reviewed for this assessment](https://www.youtube.com/watch?v=ZlKkgqW7J1o)
is retained as the catalyst for this review, not as a policy authority.
