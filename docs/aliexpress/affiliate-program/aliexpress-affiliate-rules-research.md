# AliExpress Affiliate Programme — rules, attribution and API limits for a UK affiliate catalogue

**Prepared for:** a UK-targeted affiliate catalogue site (English, GBP, plushies/toys/collectables first), linking customers to AliExpress to complete purchases. No payments taken, no orders placed, no dropshipping.

**Research date:** 30 August 2026
**Account inspected:** AliExpress Portals publisher account (signed in) and AliExpress Open Platform App Console application ID **6102** (`hydra_ae_1`, Affiliates API, Online). AppKey, App Secret, account e-mail, phone number, payee details and balances are deliberately excluded from this report.
**Actions taken:** read-only. No settings changed, no permission applications submitted, no secrets viewed or reset, no tokens generated, no test API calls made.

**Sources used:** AliExpress's own documents only — the Affiliate Program Service Agreement, the Affiliate Program Rules and Policies (behind the Portals login), the Portals Help Centre, the signed-in Portals account pages, the AliExpress Open Platform documentation and API reference, and the AliExpress Terms of Use, Privacy Policy and Cookie Notice. No blogs or third-party summaries were used or relied on.

---

## 1. Executive summary

**The programme fits the plan, with three material caveats.**

A catalogue-style affiliate site is explicitly contemplated by AliExpress. Its own Help Centre tells publishers to "start your own shopping guide in which you recommend products which can be found on AliExpress, including links and banners", and the Portals site-declaration form offers **"shopping > affiliate store"** as a channel type. The account already has a verified site of exactly that shape.

**Caveat 1 — the commission rate for this niche is 7%, and the account is currently on a model that pays nothing on non-affiliate products.** Toys, hobbies, plush toys and collectables are not named in AliExpress's category rate table, so they fall under "all other categories": **7% on Affiliate Products**, capped at **USD 50 per order**. The New Buyer Bonus for the United Kingdom is **US$0.00**. More urgently, the account's own Commission Rules page currently renders under the heading **"Non-Transparent Channels Commission Model"** — the model AliExpress applies when no Advertising Channel has been declared — under which Non-Affiliate Products earn **0%** and no New Buyer Bonus is payable at all. The My Websites page shows the site *has* now been verified and says the commission model "will be adjusted", so this looks like a pending reclassification rather than a permanent state, but it is worth confirming before building revenue assumptions.

**Caveat 2 — there is no basket hand-off, and no documented cookie window.** The AE-Affiliate API exposes 16 methods; none of them creates, populates or transfers an AliExpress basket, and no approved basket URL is documented. A local basket or shopping list on your own site is therefore the only option, and each item must be handed over as its own tracking link. Separately, AliExpress states only that it tracks "by using cookies" — **nowhere in any of its documents does it state the cookie lifetime or the attribution model** (last-click, first-click or otherwise). This is the single most commercially significant gap in AliExpress's published rules and needs a support ticket.

**Caveat 3 — three of the APIs this project needs are not currently enabled.** Standard API for Publishers and System Tool are Active on app 6102. **Advanced API** and **SKU Dimension API** are Inactive. Delivery/shipping information (`aliexpress.affiliate.product.shipping.get`), SKU-level detail (`aliexpress.affiliate.product.sku.detail.get`), hot-product queries and smart match all sit behind those two permission groups. Both require manual review; neither is auto-approved.

One further operational point that is easy to miss: **AliExpress invalidates tracking short-keys that are over a year old, or that have had no clicks for six months** (Service Agreement clause 5.4). An automated catalogue that generates a link per product and caches it forever will silently accumulate dead links.

On the reassuring side: the branding rules are strict but clear (no "AliExpress" or any variant in your domain, page titles, metadata or paid search without written consent), a neutral `.co.uk` domain is exactly the right choice, if a customer clicks a link for Product A and buys Product B **you still earn the commission on the final order**, and AliExpress offers a real-time server-to-server order push (S2S) so you do not have to poll for orders.

---

## 2. Answer table

Confidence key: **Confirmed** = stated in an AliExpress document; **Account** = observed in the signed-in account; **Inferred** = a conclusion drawn from documented API capability, flagged as such; **Unanswered** = not addressed in any AliExpress source found.

| Question | Confirmed answer | Source | Effective date | Confidence |
|---|---|---|---|---|
| **A. Commission rates by category for UK traffic** | Toys/plushies/collectables are not a named category, so "all other categories" applies: **7% on Affiliate Products**. Named categories run 3% (phones, computing, storage, home audio) to 9% (phone accessories, interior accessories, garden supplies, women's/men's/children's clothing). "Special category" (ID 200001075) is 0%. | Rules and Policies cl. 5.3.3.1(a), Table 5.3.3.1(a) | Part A, 1 Aug 2025 | Confirmed |
| Non-Affiliate Product rate | **1%** under the Default Channel and Ordinary Cashback/Extension models (both use the same table) and under Cashback/Extension Partner (cl. 5.3.7.2); **0%** under the Non-Transparent, Optimizing-A and Optimizing-B models | Tables 5.3.3.1(a), 5.3.4.1(a), 5.3.6.1(a), 5.3.8.1(a), 5.3.9.1(a); cl. 5.3.7.2 | 1 Aug 2025 | Confirmed |
| Which model applies to **this account** | The account's Commission Rules page renders under **"Non-Transparent Channels Commission Model"** → 7% Affiliate Products / 0% Non-Affiliate / no New Buyer Bonus | Portals > Payment > Commission Rules | live, 30 Aug 2026 | Account |
| Commission cap | **USD 50 per order/transaction**, in all cases | Rules cl. 5.1.1, 5.1.3 | 1 Aug 2025 | Confirmed |
| What the rate is applied to | "Transaction Price" = actual product price **excluding discounts, coupons, shipping costs, taxes, duties and third-party fees** | Rules cl. 1.34, 5.1.4 | 1 Aug 2025 | Confirmed |
| Hot-product links vs normal links | Normal link = `promotion_link_type=0` (standard commission); hot link = `promotion_link_type=2` (hot-product commission). Hot Product rates are whatever is displayed in the Hot Products section/API at the time, recorded at purchase | Rules cl. 5.3.3.1(b); `aliexpress.affiliate.link.generate` | 1 Aug 2025 / API doc 7 Jun 2022 | Confirmed |
| New-customer bonus | **US$0.00 for the United Kingdom.** Only Korea carries a bonus (US$4.5 app / US$3.0 non-app). Not payable at all under the Ordinary Cashback/Extension (cl. 5.3.4.5), Non-Transparent (cl. 5.3.6.3) or Cashback/Extension Partner (cl. 5.3.7.6) models, or for offline channels (cl. 5.1.8.1). The Rules also disclaim it for Dropshipping, but that sentence appears at the end of cl. 5.3.8.3 and 5.3.9.3 (the Optimizing-A/B sections) rather than in cl. 5.3.5 — an apparent drafting slip | Table 5.3.3.2; cl. 5.1.8.1, 5.3.4.5, 5.3.6.3, 5.3.7.6 | 1 Aug 2025 | Confirmed |
| Promotional/incentive uplift | A CPX 2.0 incentive programme exists (order API returns `incentive_commission_rate`, `estimated_incentive_paid_commission`); the public Portals home page advertises "Earn up to 4% extra commission rate for promotions that surpass targets on our incentive programs" | `aliexpress.affiliate.order.list`; Portals public home page | API doc 31 Mar 2022; page live | Confirmed (terms are per-campaign) |
| Excluded products | Virtual products (gift cards, coupons, stored value), books, travel services, products **not** listed by sellers registered in mainland China, Spain, Russia, Turkey or Italy, and products from those sellers that do not participate in the affiliate programme | Rules cl. 5.1.5 | 1 Aug 2025 | Confirmed |
| Excluded/special sellers | Certain store IDs attract special rates of 1%, 2% or 0% regardless of category or channel model | Rules cl. 5.3.10; Portals > Payment > Commission Rules note 5 | 1 Aug 2025 | Confirmed |
| Specific-product overrides | A monthly "Specific Product List" (product IDs, rates, validity, regions) published by the 10th of each month, effective 3 days later, **overrides everything else** | Rules cl. 5.3.11 (new in Part A) | 1 Aug 2025 | Confirmed |
| Validation period / order lifecycle | Payment Completed → Buyer Confirmed Receipt → Completed Settlement. Settlement begins only when the buyer confirms receipt, and the settlement task is run manually by AliExpress operations | `aliexpress.affiliate.order.list` status definitions; Help Centre Glossary item 11 | API doc 31 Mar 2022 | Confirmed |
| Settlement delay | Commission is paid into Account Balance **in the month following the Completed Order Time**, monthly after a whole month has ended; the New User Guide says "around the 25th of every month" | Help Centre Top Question 12 & 13; New User Guide 3; Service Agreement cl. 7.1 | live / 31 Mar 2022 | Confirmed |
| Minimum payout | **USD 16.00 minimum withdrawal with a USD 15.00 processing fee** (Help Centre, newest). The Service Agreement cl. 7.5 says the remitting balance must exceed USD 15. Both are recorded in §3 below | Help Centre Top Question 14; Service Agreement cl. 7.5 | live / 31 Mar 2022 | Confirmed (documents conflict) |
| Payment methods | **International bank transfer only.** Commissions paid in US dollars unless otherwise agreed | Help Centre Top Question 15; Rules cl. 5.1.6 | live / 1 Aug 2025 | Confirmed |
| What invalidates commission | Buyer refunds and cancellations; order risk; failed or overdue anti-spam/penalty appeals; orders unsettled more than **180 days** after Completed Payment Time (e.g. stuck in dispute); any order made in breach of the Rules; anything AliExpress deems fraudulent or invalid | Order API status definitions; Rules cl. 5.1.2; Service Agreement cl. 7.6–7.7 | 1 Aug 2025 | Confirmed |
| Are rates account-specific? | **Yes.** The rate set depends on the "Main Advertising Channel Type", which **AliExpress determines at its sole discretion** and may change. Insertion Orders and invitation-only models can override | Rules cl. 5.1.1.1, 5.3.1.1–5.3.1.3 | 1 Aug 2025 | Confirmed |
| **B. Cookie / attribution window** | AliExpress states only "we track completed transactions by using cookies… if he deletes his cookies, we will not be able to track the transaction". **No duration is stated anywhere.** | Help Centre Top Question 2 | live | **Unanswered** |
| Attribution model (last/first click) | **Not stated in any AliExpress document.** The order API and S2S return a `url` field described as "IP page where the order is attributed to the click", which confirms click-based attribution but not the tie-break rule | S2S Guidance field table; `aliexpress.affiliate.order.list` | live | **Unanswered** |
| If another affiliate's link is clicked later | Not addressed in any AliExpress document | — | — | **Unanswered** |
| Click Product A, buy Product B | **You are still paid.** "We count the commission based on the final order… If you promote Product A while the transaction completed is for Product B within the tracking path, we will still provide you with the commission for the order." | Help Centre Top Question 10 | live | Confirmed |
| Several sellers in one checkout | Orders are reported and settled per **sub-order**; the order API's `sub_order_id` is the operative ID and S2S pushes `order_id` = "Sub-order ID". A multi-seller basket therefore splits into separately tracked, separately settled sub-orders | `aliexpress.affiliate.order.list`; S2S Guidance | 31 Mar 2022 / live | Inferred (from documented data model) |
| Web vs mobile-web vs app | The Rules distinguish "AliExpress Applications" (apps) from "AliExpress Website / PC / Msite", and the New Buyer Bonus table splits "buyers from APP" vs "buyers not from APP". No separate attribution rule is stated | Rules cl. 1.8, 1.9; Table 5.3.3.2 | 1 Aug 2025 | Confirmed (bonus only); Unanswered (attribution) |
| Do tracking/sub IDs survive redirects and app hand-off? | Not stated. AliExpress documents that `af`, `cn`, `cv`, `dp` are recorded and returned in the Live Order Tracking export and in S2S payloads, which implies survival to order level | Top Question 4; S2S Guidance | live | Inferred |
| Documented deep-link format | Yes: `https://s.click.aliexpress.com/e/<shortkey>`, with optional custom strings appended after `?` — `af`, `cn`, `cv`, `dp` (names are fixed, use any subset). `af` is restricted to Network-type accounts with filed sub-affiliates | Help Centre Top Question 4 | live | Confirmed |
| Documented app-link scheme | **No separate app-link/deep-link scheme is documented.** The `s.click` short link is the only documented format | — | — | **Unanswered** |
| Link expiry | AliExpress may invalidate a tracking short-key that is **over one year old**, or **under one year old but with no clicks for six months**, or that is suspected of pointing at infringing content | Service Agreement cl. 5.4 | 31 Mar 2022 | Confirmed |
| **C. Any API/URL that adds to an AliExpress basket** | **No.** The AE-Affiliate category exposes 16 methods (link generation, product query/detail/SKU/shipping, hot products, smart match, categories, featured promos, three order methods, business licence lookup). None creates or populates a basket | AliExpress Open Platform API reference, category "AE-Affiliate" (cid 21407) | retrieved 30 Aug 2026 | Inferred (exhaustive review of the published method list) |
| Multi-product transfer in one operation | `aliexpress.affiliate.link.generate` accepts **up to 50 source links per request** (API reference); `aliexpress.affiliate.productdetail.get` accepts up to 50 product IDs (Affiliate API Guidance §3). These are batch *link/data* operations, not basket transfer | API reference; Help Centre Affiliate API Guidance §3 | 7 Jun 2022 / live | Confirmed |
| So only individual product links are supported? | Yes. The Link Generator accepts "product detail pages, homepages, venue pages or store pages" | Help Centre "How to generate tracking links?" | live | Confirmed |
| Local basket / wish list on the publisher's own site | **Not addressed by any AliExpress rule.** Nothing prohibits it; the prohibitions on overlays, iframes and interference are aimed at interfering with *AliExpress's own pages* | Rules cl. 3.6.3, 3.7.13 | 1 Aug 2025 | **Unanswered** (permitted by absence) |
| Required wording / UI treatment for hand-off | **No wording or UI requirement is specified anywhere in AliExpress's affiliate documents** | — | — | **Unanswered** |
| **D. Shop-like catalogue / "superstore" allowed?** | **Yes.** "On your own website: Start your own shopping guide in which you recommend products which can be found on AliExpress, including links and banners." The site-declaration form offers channel type "shopping > affiliate store" | Help Centre Top Question 8 / New User Guide 4; Portals > Account > My Websites | live | Confirmed |
| Must the site make clear it is not AliExpress? | Yes, in effect: the site must not contain anything "likely to confuse or mislead others into forming any untrue association with AliExpress" or misrepresent affiliation | Rules cl. 3.3.3, 3.4.4, 3.5.4 | 1 Aug 2025 | Confirmed |
| Required affiliate-disclosure wording | **AliExpress specifies none.** No disclosure wording appears in the Service Agreement, the Rules and Policies or the Help Centre | — | — | **Unanswered** |
| Use of "AliExpress", trademarks, logo | Prohibited without **prior written approval**, including abbreviations ("AE", "AliE"), reorderings, translations, case/font/colour variants and combinations with other words ("AliClothing", "AE UK"). Where approved, the content must carry a working hyperlink to the AliExpress Platform | Rules cl. 2.2, 3.1.1 | 1 Aug 2025 | Confirmed |
| "AliExpress" in a domain or subdomain | **Prohibited** without written consent — including domains that imitate AliExpress URLs or are confusingly similar | Rules cl. 3.2.1, 3.2.2, 3.3.1, 3.3.2 | 1 Aug 2025 | Confirmed |
| "AliExpress" in page titles, metadata, paid search | Advertisements and advertising materials must not contain Restricted Marks or variants without written consent; **SEO or SEM campaigns using the marks are prohibited outright**, explicitly on Google, Facebook, Bing, Yahoo and Yandex | Rules cl. 3.5.1, 3.7.7 | 1 Aug 2025 | Confirmed |
| Neutral domain with several themed shops under URL paths | **Not addressed.** Sites are declared individually (Add site, one marked Primary), and the *first* site/channel/URL declared is treated as your declared main Advertising Channel | Rules cl. 5.3.1.3; Portals > Account > My Websites | 1 Aug 2025 | **Unanswered** |
| Framing, redirects, URL masking, cloaking | Prohibited: iframes, overlays, cookie stuffing/dropping, pop-ups and pop-unders, notifications, floating screens, hijacked browsers or URLs, modifying AliExpress URL parameters | Rules cl. 3.6.3, 3.7.6.2, 3.7.6.3, 3.7.13 | 1 Aug 2025 | Confirmed |
| Copying product descriptions, reviews, ratings, images | AliExpress Content is licensed only for advertising through the Programme, and must not be transferred, copied, modified, adapted or made into derivative works without written consent. Separately, the site Terms of Use prohibit systematic retrieval by robots/spiders to build a database without written permission — **the affiliate API is the authorised route** | Service Agreement cl. 8.2, 8.4; Rules cl. 1.4; Terms of Use cl. 3.2(a) | 31 Mar 2022 / 1 Aug 2025 / ToU 2026 | Confirmed |
| Refreshing stale prices/availability/coupons/delivery | **No refresh or removal obligation is stated.** AliExpress says API data is queried in real time on each call, and featured-promo product pools "are updated daily" | Help Centre Affiliate API Guidance 4.1, 3 | live | **Unanswered** |
| Must displayed prices include VAT/shipping? | **No display rule exists in the affiliate documents.** Note the commission base excludes shipping, taxes and duties | Rules cl. 1.34 | 1 Aug 2025 | **Unanswered** |
| Restrictions on children/toys/collectables/counterfeits/unsafe/adult | No toy- or child-safety-specific rule. Prohibited content includes pornographic or obscene material (expressly including content depicting the exploitation of minors), weapons, drugs, tobacco, gambling, and anything infringing third-party IP. A separate rule set bans promoting trademark-, copyright- and patent-infringing products and "hidden links", with permanent account termination and forfeiture of funds as remedies | Rules cl. 3.5.9–3.5.15; Rules Against Promotion of Illegal Products | 1 Aug 2025 / 13 May 2025 | Confirmed |
| Email, social, paid search, SEO permitted? | Yes in principle — the Service Agreement's definition of Publisher expressly covers pop-up links, SEM links, SEO links and email links, and the Help Centre lists chat, blogs, forums, own website, email and social. **SEM is acceptable excluding trademarked keywords, and "all other marketing methods will need to be approved by AliExpress.com in advance"** | Service Agreement cl. 1.12; Help Centre Top Question 8, 9 | 31 Mar 2022 / live | Confirmed |
| Bidding on AliExpress or seller trademarks | Prohibited for AliExpress marks (cl. 3.7.7). Seller trademarks are not named, but promoting trademark-infringing products is separately prohibited | Rules cl. 3.7.7; Illegal Products Rules 2(a) | 1 Aug 2025 / 13 May 2025 | Confirmed (AliExpress marks); Unanswered (seller marks in paid search) |
| Auto-generated / thin pages / false scarcity | Not named. The nearest rules prohibit content that is "misleading or fraudulent", content "stolen or misappropriated from other websites", and content contrary to public interest | Rules cl. 3.5.17, 3.5.18, 3.5.19 | 1 Aug 2025 | Partly confirmed |
| **E. Buying through your own links** | **Prohibited.** "engaging, directly or indirectly, or authorizing any dishonest or fraudulent transactions, whether by yourself or together with your associates (including but not limited friends, relatives, colleagues…) … making purchases on the AliExpress Platform through designated links … to obtain unlawful or unjust commissions" | Rules cl. 3.7.4 | 1 Aug 2025 | Confirmed |
| Employees, contractors, related companies | Covered by the same clause ("associates … colleagues"), and cl. 3.7.5 prohibits collaborating with sellers for unjust commission; cl. 3.7.10 prohibits a seller joining the Programme to earn commission on their own sales | Rules cl. 3.7.4, 3.7.5, 3.7.10 | 1 Aug 2025 | Confirmed |
| Test purchases | **Not addressed.** No carve-out for testing appears anywhere | — | — | **Unanswered** |
| Artificial traffic / cookie stuffing / forced clicks / incentivised traffic | Defined and prohibited: programs or scripts generating abnormal or artificial views/clicks; pop-up and pop-under advertising; viruses, fictitious webpages, hijacked browsers or URLs; malicious plug-ins; modifying AliExpress source code or URL parameters; cookie stuffing, cookie dropping, iframes, PPV pop-ups; using rewards or incentives "as a bait"; resource exchange or barter as an advertising tactic. The Service Agreement defines "Fraud" as any action intentionally creating sales, leads or click-throughs using robots, frames, iframes, scripts or manual page refreshing solely to create commissions | Rules cl. 3.6.3, 3.7.3, 3.7.6, 3.7.13, 3.8; Service Agreement cl. 1.7; Types of Violation & Case Study | 1 Aug 2025 / 31 Mar 2022 | Confirmed |
| Asking friends or existing customers to use your link | The Help Centre encourages recommending AliExpress to "friends and colleagues" and sharing links by e-mail and social media, **but** cl. 3.7.4 prohibits associates purchasing through your links to obtain unjust commission. Both texts are recorded in §6 | Help Centre Top Question 8; Rules cl. 3.7.4 | live / 1 Aug 2025 | Confirmed (documents in tension) |
| **F. Exact daily and per-second API quotas (app 6102)** | **None are published.** The App Overview's "API Call Limit" field is **blank**; the app record carries no traffic rules. App Statistics reports an "Average QPS" column but states no ceiling. The QPS table in the Open Platform docs belongs to the **DropShippers** API set and does not cover affiliate methods | App Console > App Overview, App Statistics | live, 30 Aug 2026 | Account / **Unanswered** |
| Per-method quotas | None published for affiliate methods. Functional limits exist: 50 products per page, max 100 pages, **max 5,000 products per query**; `productdetail.get` max 50 IDs; `link.generate` max 50 links | Help Centre Affiliate API Guidance 4.2; API reference | live / 2022 | Confirmed |
| Current permissions | **Standard API for Publishers — Active. System Tool — Active.** Advanced API — Inactive. SKU Dimension API — Inactive | App Console > App Overview | live | Account |
| How to get Advanced API | Apply in App Console > API Permission Group > Advanced API. Its description instructs: "Please provide your register email on portals when apply for this API." Manual review (not auto-approved). Affiliate API Guidance §2.2 lists `product.shipping.get`, `product.sku.detail.get` and `product.smartmatch` as requiring Advanced permissions; the §3 function table adds `hotproduct.query` and `hotproduct.download`. **The console's own Advanced API description names only "hot products query and smart match api" and attributes SKU detail to the separate SKU Dimension group — the two AliExpress descriptions conflict** | App Console; Help Centre Affiliate API Guidance §2.2 and §3 | live | Confirmed (with a documented conflict) |
| How to get SKU Dimension API | Apply in the same place; covers the SKU Product Detail Info API. Manual review | App Console | live | Confirmed |
| Do affiliate APIs need OAuth tokens? | The console shows OAuth2.0 Server-side is configured. However AliExpress's own affiliate signature example contains **no `access_token`**, the API reference marks `access_token` as not required on affiliate methods, and `app_signature` is documented as optional. This points to signed AppKey/AppSecret calls being sufficient for the affiliate product/link/order methods | Help Centre Affiliate API Guidance 4.1 & 4.5; API reference common-parameter tables; App Console > Auth Management | live | **Inferred** — confirm with support before building |
| Token lifetimes | **Access token 30 days; refresh token 60 days** (this account). Platform docs add that a test-status app gets 1 day / 2 days, extended on going online, and that refreshing resets the access token but not the refresh token | App Console > Auth Management; Open Platform authentication docs | live / 2024 | Account + Confirmed |
| Rate-limit response codes and retry behaviour | **Not documented for affiliate APIs.** The platform documents a three-way error taxonomy (SYSTEM / ISV / ISP) and advises checking "authority, frequency and other conditions" on SYSTEM errors; `ServiceUnavailable` is documented on the System Tool APIs. The "sleep for 1–2 seconds and request again" guidance is in the **DropShippers** FAQ and does not govern affiliate methods | Open Platform "Error messages"; System Tool API error codes | 2024 | **Unanswered** |
| Automated scheduled catalogue ingestion | Permitted by design: the API is described as "Channel-initiated Pull… the developer can initiate queries at any time", and AliExpress publishes a documented method for walking an entire result set by price-banding to stay under the 5,000-per-query limit | Help Centre Affiliate API Guidance 1, 4.2 | live | Confirmed (by description; no explicit permission clause) |
| Product-data storage, caching, retention, deletion | **No storage, caching or retention rule exists.** The only adjacent constraints are the licence limits on AliExpress Content (cl. 8.2/8.4), the ban on systematic robot retrieval *of the site* in the Terms of Use, and the Personal Data restrictions in Rules cl. 4 | — | — | **Unanswered** (for product data) |
| Order-reporting webhooks vs polling | **Both exist.** S2S (Server to Server) pushes an order message in near real time after the buyer pays, configured at Portals > Tools > S2S Setting, with a documented field list. Polling is via `aliexpress.affiliate.order.list` / `.listbyindex` / `.get`. Note S2S fires on **payment**, not on settlement, so settlement status still needs polling | Help Centre S2S Guidance; API reference | live | Confirmed |
| **G. Visitor data passed to AliExpress on click** | **Not enumerated by AliExpress.** What is documented is that the tracking link carries the `tracking_id` and any `af`/`cn`/`cv`/`dp` values you append | Help Centre Top Question 4; S2S Guidance | live | **Unanswered** |
| Who sets the tracking cookie | AliExpress. "We track completed transactions by using cookies"; the Cookie Notice lists first-party `aliexpress.com` cookies and says cookies are used for "assisting our partners in tracking user visits to the Platforms". **AliExpress requires no cookie to be set by the publisher's site** | Help Centre Top Question 2; Cookie Notice | live / 16 May 2024 | Confirmed |
| Cookie-consent or privacy wording required of publishers | **AliExpress prescribes none.** It requires only that publishers comply with applicable data protection law and obtain written consent before processing any Personal Data in connection with the Programme | Rules cl. 4.1, 4.2; Service Agreement cl. 6.2(b)(ii) | 1 Aug 2025 | Confirmed (no wording specified) |
| Data-processing / international-transfer terms for a UK publisher | The Service Agreement's Schedule 2 Data Processing Addendum expressly names the **United Kingdom Data Protection Act 2018** in "Applicable Data Protection Law", treats publisher and AliExpress as **separate and independent controllers** (never joint controllers), and requires transfers outside the EEA to take "such measures as are necessary", naming adequacy, BCRs and the **(EU) 2021/914 controller-to-controller Standard Contractual Clauses** as examples. The Rules add that no controller–processor relationship is implied. For EEA/UK visitors, the data controller is Alibaba.com Singapore E-Commerce Private Limited | Service Agreement Schedule 2 cl. 1.2, 3.1, 4.2; Rules cl. 4.3; Privacy Policy §J, §N | 31 Mar 2022 / 1 Aug 2025 / 30 Jul 2026 | Confirmed |

---

## 3. Commission and attribution findings

### 3.1 How the rate is chosen

AliExpress does not publish one rate card. Clause 5.3.2.1 enumerates **seven commission models** (a)–(g), applied according to your "Main Advertising Channel Type" — of which there are five (cl. 1.19) — plus two "Optimizing" models that AliExpress applies when it rates your promotion quality as needing improvement:

| Model | Applies when | Affiliate Products — "all other categories" | Non-Affiliate Products | New Buyer Bonus |
|---|---|---|---|---|
| Default Channel (cl. 5.3.3) | You have declared a main channel that is not cashback/extension, dropshipping or non-transparent | **7%** | 1% | Per Table 5.3.3.2 |
| Ordinary Cashback/Extension (cl. 5.3.4) | Cashback, loyalty-points or browser-extension channels | 7% (same table as Default) | 1% (same table) | **None** |
| Dropshipping (cl. 5.3.5) | Purchases made for resale on third-party platforms | Rate matches what merchants set in the affiliate programme | — | **None** |
| Non-Transparent (cl. 5.3.6) | **No channel declared at the Portals site** | 7% (Hot Products at the same rate) | **0%** | **None** |
| Cashback/Extension Partner (cl. 5.3.7) | Invitation only | Per invitation (Table 5.3.4.1(a) for ordinary Affiliate Products) | 1% | **None** (cl. 5.3.7.6) |
| Optimizing-A (cl. 5.3.8) | "Quality of your promotion is in rating A and needs to be improved" | **1%** | 0% | — |
| Optimizing-B (cl. 5.3.9) | "Quality of your promotion is in rating B and needs to be improved" | **6%** | 0% | — |

Crucially, **AliExpress assigns the type, not you** (cl. 5.3.1.2), weighing your declared channel, the split of transaction value, commissions, bonuses and traffic across channel types, your target markets, and "your breach or violations". It "shall be entitled to change your Main Advertising Channel Type… at its sole reasonable discretion". Clause 5.3.1.3 adds that **the first site/channel/URL on your account is treated as your declaration** of main Advertising Channel — which matters if you intend to add several themed shops.

**Account-specific:** Portals > Payment > Commission Rules currently renders under the heading **"Non-Transparent Channels Commission Model"**, showing 7% on Affiliate Products in "Other Categories (ID:-1)" and 0.0% on Non-Affiliate Products across every category. That page's own note 4 gives the remedy: *"To avoid being deemed as a Non-Transparent Channel, please fill in the required information and verify your email on AliExpress Portal via 'settings > my websites'."* The My Websites page shows one Primary site, verified, declared as channel type **shopping > affiliate store**, category **Toys & Hobbies**, promotional attribute **Non-network**, with the banner *"The site information has been successfully verified. Your commission model will be adjusted based on the most recent information you have submitted."* So the classification appears to be mid-adjustment. **Re-check the Commission Rules page before modelling revenue** — under the Default Channel model non-affiliate products earn 1% rather than 0%, and the New Buyer Bonus becomes theoretically available (though it is US$0.00 for the UK regardless).

### 3.2 Category rates that matter for plushies, toys and collectables

Table 5.3.3.1(a) names fifteen categories. Toys, hobbies, plush toys and collectables are **not among them**, so they take the **"all other categories"** row: **7%** on Affiliate Products, 1% on Non-Affiliate Products (Default Channel) or 0% (the model currently shown on this account).

The only nearby named category is **children's clothing (ID 311, 9%)**. If part of the catalogue drifts into dress-up or clothing lines, those items may attract the higher rate. "Special category" (ID 200001075) pays **0%** in every model — worth excluding at ingestion time.

Full transcriptions of all five rate tables, with the source image URLs so they can be re-verified, are in `sources/10-commission-rate-tables-transcribed.md`. These tables are published only as images inside the Help Centre document; the surrounding text does not repeat the numbers.

### 3.3 What reduces the payable amount

- **A hard cap of USD 50 per order or transaction** (cl. 5.1.1, 5.1.3), regardless of basket size.
- The rate is applied to **Transaction Price**, defined as the actual product price **excluding discounts, coupons, shipping costs, taxes, duties and third-party fees** (cl. 1.34). A £40 plush with £6 shipping and VAT earns commission on the product line only.
- In the EU, "Phone & Telecommunications" purchases can carry a third-party service fee of up to 8% which is stripped out of the Transaction Price before commission (cl. 5.1.4). Not relevant to this niche, but it establishes the pattern.
- Certain seller store IDs override everything with 1%, 2% or 0% (cl. 5.3.10). The list lives at Portals > Payment > Commission Rules.
- The monthly **Specific Product List** (cl. 5.3.11, new in the August 2025 version) overrides all other rates for named product IDs, is published by the 10th of each month and takes effect three days later.

### 3.4 Settlement and payment

The order lifecycle is documented identically in the order API and the Help Centre Glossary:

1. **Payment Completed** — buyer paid successfully.
2. **Buyer Confirmed Receipt** — "This status only change when:Buyer confirms receipt and settlement task begins which is manually executed by our operation team" (verbatim, including the source's grammar).
3. **Completed Settlement** — "Orders have been verified and commission has been paid."
4. **Invalid** — "Orders will not be settled including buyer refunds, order risks, antispam/penalty appeal failed, antispam/penalty appeal overdue, order not settled being over 180 days apart from the Completed Payment Time (such as in abnormal state like dispute)."

Payment timing: *"The commission for orders in Completed Order status will be paid into Account Balance in the month next to the Completed Order Time"* and *"the commission paid into Account Balance is based on the Completed Orders monthly after a whole month ended"* (Top Question 12–13). The New User Guide phrases the same thing as *"paid to your account around the 25th of every month"*. The Service Agreement cl. 7.1 says AliExpress pays "monthly for the Services delivered in the previous month".

**A documented conflict on the withdrawal threshold**, all three recorded here:

| Source | Text | Date |
|---|---|---|
| Help Centre, Top Question 14 (newest) | "Each withdrawal through wire transfer method requires a minimum threshold of **USD 16.00** and includes a processing fee of **USD 15.00**." | live |
| Help Centre, New User Guide 3 | "The withdrawal balance must be over **$15** as a **$15** service fee will be charged for each withdrawal. The $15 will be deducted regardless of whether the withdrawal is successful or not." | live |
| Service Agreement cl. 7.5 | "…provided such remitting balance exceeds **USD15**." | 31 Mar 2022 |

The Top Question figure is the most specific and appears on the same live Help Centre, so it is the one to plan against; but note that on the smallest withdrawals the fee consumes almost the entire balance. Payment method is **international bank transfer only** (Top Question 15), in **US dollars** (Rules cl. 5.1.6), and the publisher bears all taxes and bank handling fees (Service Agreement cl. 7.8–7.10).

### 3.5 Reversal and clawback

Beyond the "Invalid" states above, AliExpress may set off a "Chargeback Amount" against future months, demand repayment, or deduct from the account balance (Service Agreement cl. 7.6–7.7), and Rules cl. 6.1 lists lettered enforcement measures a. to m. (the letter "m." appears twice in the source, so there are fourteen items) including withholding payment, forfeiting commissions, invalidating channel IDs, stopping tracking on promotion links, and terminating the account. Appeals must be filed **within 5 working days** of the enforcement action (cl. 6.3.1), with a determination promised within 5 days; the Help Centre routes appeals through Portal > Payment > Penalty Center and quotes a 7-working-day review. Violation records are retained for a maximum of 90 days (cl. 6.6).

Under the separate **Rules Against Promotion of Illegal Products** (effective 13 May 2025) the consequences are harsher and explicitly cumulative: permanent account termination, cancellation of unsettled commission, permanent prohibition on withdrawals **including amounts already settled**, and forfeiture of all remaining funds — extendable to "Associated Accounts" identified by device information, login credentials or payment details.

### 3.6 Attribution — what is and is not documented

**Documented:**
- Tracking is by cookie, set by AliExpress: *"We track completed transactions by using cookies. If a buyer, who came through your site, makes a purchase on AliExpress, you will receive a commission. However, if he deletes his cookies, we will not be able to track the transaction."*
- **Cross-product attribution works in your favour.** Top Question 10 asks precisely the question this project raises — *"If a purchase is made for Product B via a promotional link intended for Product A, will the commission still be paid out?"* — and answers: *"Yes, we count the commission based on the final order… we will still provide you with the commission for the order."* A catalogue whose job is to start a session, not to predict the exact SKU, is therefore economically viable.
- Multi-seller checkouts resolve to **sub-orders**. The order API deprecates `order_id`/`order_number` in favour of `sub_order_id`, and S2S pushes `order_id` described as "Sub-order ID". Each sub-order carries its own `product_id`, `commission_rate`, `paid_amount` and status.
- **Sub-ID format:** append to the short link after a question mark — `https://s.click.aliexpress.com/e/abcD12345?af=001&cn=002&cv=003&dp=abc`. `cn` = campaign, `cv` = creative, `dp` = free-form dynamic parameter, `af` = sub-affiliate ID (Network accounts with filed sub-publishers only). The names are fixed; any subset may be used. Values come back in the Live Order Tracking CSV export and in S2S payloads.
- Up to **50 Tracking IDs** per account, set in Account > Tracking ID, one flagged default. **They cannot be edited or removed once added.**
- App vs non-app is distinguished for the New Buyer Bonus and in the `order_platform` field, which separates `influencer_platform` from `affiliate_platform`.

**Not documented anywhere:** the cookie lifetime; whether attribution is last-click, first-click or something else; what happens when a second affiliate's link is clicked afterwards; whether the tracking cookie survives a hand-off into the AliExpress app; and whether any app-link scheme exists beyond the `s.click` short link. These are listed in §11 with the exact channel to resolve each.

---

## 4. Basket and checkout findings

**There is no basket API and no documented basket URL.** The AE-Affiliate category (cid 21407) publishes exactly sixteen methods:

| Purpose | Methods |
|---|---|
| Links | `aliexpress.affiliate.link.generate` |
| Products | `aliexpress.affiliate.product.query`, `aliexpress.affiliate.productdetail.get`, `aliexpress.affiliate.product.sku.detail.get`, `aliexpress.affiliate.product.shipping.get`, `aliexpress.affiliate.product.smartmatch` |
| Hot products & promos | `aliexpress.affiliate.hotproduct.query`, `aliexpress.affiliate.hotproduct.download`, `aliexpress.affiliate.featuredpromo.get`, `aliexpress.affiliate.featuredpromo.products.get`, `aliexpress.affiliate.promotion.info.get` |
| Taxonomy | `aliexpress.affiliate.category.get` |
| Orders | `aliexpress.affiliate.order.get`, `aliexpress.affiliate.order.list`, `aliexpress.affiliate.order.listbyindex` |
| Other | `/aliexpress/xinghe/merchant/license/get` (business licence lookup) |

None of these adds an item to a basket, creates a basket, or produces a multi-item checkout URL. This is a conclusion from an exhaustive review of the published method list, not from a statement by AliExpress — AliExpress nowhere says "there is no basket API".

**What batching does exist** is link and data batching, not basket transfer: `link.generate` takes "original link or value; max 50 link in one request", and `productdetail.get` accepts up to 50 product IDs per call. So a 12-item local basket becomes one `link.generate` call returning 12 tracking links — twelve separate hand-offs for the customer, not one.

**What the Link Generator accepts:** *"you can enter URLs of AliExpress pages you want to promote, including product detail pages, homepages, venue pages or store pages"*. Store and venue pages are the closest thing to a multi-product landing, and they are legitimate targets — but they are AliExpress's pages, not your basket.

**A local basket on your own site is not addressed by any AliExpress rule.** Nothing in the Service Agreement, the Rules and Policies or the Help Centre prohibits holding a wish list or shopping list before sending the customer on. The prohibitions that sound adjacent — cl. 3.6.3 and 3.7.13 on overlays, iframes, cookie stuffing and interference — are all directed at interfering with **AliExpress's own web pages**, not at what you build on your own domain. Nor is there any prohibition on a "superstore" presentation; the opposite, in fact (§5.1).

**No wording or UI treatment is specified.** AliExpress does not tell publishers what to say at the point of hand-off, does not require a "you are leaving our site" interstitial, and does not mandate any label on the button. The only constraint that bites is the general one: nothing on the page may be "likely to confuse or mislead others into forming any untrue association with AliExpress" (cl. 3.4.4). A basket that looks like it checks out on your site, and then silently redirects, is the shape most likely to attract that clause — but AliExpress has not said so, and this report does not offer a view on what UK consumer law requires.

---

## 5. Domain, branding and publisher restrictions

### 5.1 A catalogue site is explicitly contemplated

Two independent AliExpress texts endorse the model:

- Help Centre, Top Question 8 and New User Guide 4: *"**On your own website**: Start your own shopping guide in which you recommend products which can be found on AliExpress, including links and banners."*
- The Portals site-declaration form offers **"shopping > affiliate store"** as a channel type — and this account's verified site is registered under it, in category Toys & Hobbies.

The Portals home page also lists the publisher archetypes AliExpress recruits: influencers, chat groups, content/blog sites, coupon sites, price-comparison sites, cashback sites, networks and agencies, and deal sites.

### 5.2 The trademark rules are the strictest part of the programme

Clause 3.1.1 defines "Varied Restricted Marks" extraordinarily broadly. Prohibited without prior written approval:

- adding, removing or changing letters or symbols (*"AExpress"*);
- reordering them (*"ExpressAli", "BabaAli"*);
- **abbreviations — the Rules name "AE" and "AliE" explicitly**;
- combining marks with country or region names (*"AE US"*) or with new words (*"AliClothing"*);
- changing case, weight, font, colour, size, angle, proportion or resolution of the logo;
- transliterating or translating the marks.

And clause 3.1.1's closing paragraph extends "improper use" to *"promote, advertise, bid, purchase for promotion and/or advertisement of, placing the Restricted Marks and/or Varied Restricted Marks on any publically viewable websites and/or platforms, such as, Google, Yandex, Facebook, social media platforms."*

**Applied to the plan:**

| Proposed use | Position |
|---|---|
| "AliExpress" in the `.co.uk` domain | **Prohibited** without written consent (cl. 3.2.1, 3.3.1) |
| "AliExpress" in a subdomain | Same in effect. Clauses 3.2.1 and 3.3.2 speak of "domain names" and do not mention subdomains specifically, so this is a reading rather than an explicit rule |
| "AliExpress" in page titles or meta description | These are advertising material under cl. 3.5; the marks may not appear without written consent (cl. 3.5.1) |
| "AliExpress" as a paid-search keyword | **Prohibited outright** — cl. 3.7.7 bans SEO and SEM campaigns using the marks, naming Google, Facebook, Bing, Yahoo and Yandex |
| "AE" or "AliE" as a shorthand anywhere | Prohibited — expressly enumerated as Varied Restricted Marks (cl. 3.1.1.3) |
| A neutral `.co.uk` domain with no marks | **Permitted**, and the correct choice |
| AliExpress product imagery via the API | Permitted for advertising through the Programme; may not be copied, modified or made into derivative works without written consent (Service Agreement cl. 8.2, 8.4) |
| Seller imagery | Same treatment — it is "Third Party Content" licensed by sellers, and AliExpress expressly disclaims any warranty that it is non-infringing (Service Agreement cl. 8.6.2–8.6.4) |

If you ever do want to name AliExpress on the site, cl. 2.2.1 sets out the route: notify AliExpress in writing beforehand with the purpose and manner of use, obtain written approval, and — where approved — include a working hyperlink to the AliExpress Platform (cl. 2.2.2.2).

### 5.3 Several themed shops under one neutral domain

**Not addressed.** What is documented:

- Sites are declared individually in Portals > Account > My Websites, with an **Add site** control and one site flagged **Primary**.
- Clause 5.3.1.3: *"the first site/channel/URL at the account of the Participant at the Site will be deemed as the declaration of your main Advertising Channel"*, and you warrant that declaration is "true, accurate, complete, effective, most up-to-date". Violating that clause lets AliExpress impose penalties **and change your Main Advertising Channel Type**.
- Anything not declared falls into **Non-Transparent Channels** (cl. 1.23), which costs you the Non-Affiliate rate and the New Buyer Bonus.

The safe reading is that each themed shop path is a channel that ought to be declared, and that the first one declared sets the commission model for the account. Whether AliExpress accepts several path-based shops as separate sites, or treats them as one, is a question for support (§11).

### 5.4 Cloaking, framing and redirects

Prohibited by cl. 3.6.3, 3.7.6 and 3.7.13: iframes, overlays, coverings, alterations or interference with any AliExpress page; cookie stuffing and cookie dropping; PPV pop-ups, pop-ups and pop-unders in any form; notifications, pushes and floating screens; hijacked browsers or URLs; hijacking the AliExpress front page; malicious plug-ins or unauthorised software; and **modifying the source code or URL parameters of the AliExpress Platform**. Ordinary server-side redirects from your own pages to `s.click.aliexpress.com` are not what these clauses describe, but URL masking that hides the destination sits uncomfortably close to cl. 3.5.18 ("misleading or fraudulent").

### 5.5 Content reuse and data freshness

The licence is narrow. "AliExpress Content" is defined (Rules cl. 1.4) as content made available *"through the Participant's account at the Site or through Participant's authorized use of designated APIs of the Site"* — the API is the authorised channel. Service Agreement cl. 8.2 then says you *"agree not to transfer, copy, modify, alter, adapt or create derivative works based on the AliExpress Content"* without written consent, and cl. 8.4 forbids copying or modifying icons, buttons, banners and graphics files.

Separately, the AliExpress **Terms of Use** cl. 3.2(a) prohibits *"Systematic retrieval of Site Content from the Sites to create or compile, directly or indirectly, a collection, compilation, database or directory (whether through robots, spiders, automatic devices or manual processes) without written permission"*. Scraping aliexpress.com to build the catalogue is therefore out; pulling the same data through the affiliate API is the sanctioned route.

**No freshness obligation is stated.** AliExpress asserts its side is fresh — *"When the developer invokes API, the affiliate material service will query all information in real time to ensure that the information is accurate and real-time"* — and notes featured-promo product pools "are updated daily". Nothing tells publishers how often to re-poll or when to remove stale prices, coupons or delivery estimates. Likewise **no rule requires displayed prices to include VAT or shipping**; the only related fact is that commission is calculated on a Transaction Price that excludes them.

### 5.6 Category and product-type restrictions relevant to toys and collectables

There is **no toy-safety, age-rating or child-directed-marketing rule** in the affiliate documents. What exists:

- Clause 3.5.9 prohibits pornographic or obscene material, expressly including *"contents that depict the exploitation of minors"*.
- Clause 3.5.15 prohibits content infringing third-party IP; 3.5.17 content "stolen or misappropriated from other websites"; 3.5.18 content "misleading or fraudulent".
- Clauses 3.5.12–3.5.14 cover drugs, tobacco and weapons.
- Clause 3.3.6 prohibits discriminatory content.
- The **Rules Against Promotion of Illegal Products** (13 May 2025) prohibit promoting trademark-, copyright- and patent-infringing products and the use of "hidden links, misleading strategies or any other deceptive tactics".

Counterfeits are the live risk in a plush/collectables niche, and AliExpress treats them severely. The Help Centre's "Hidden Link Violation" case study describes exactly the pattern to guard against: a listing that appears to sell one thing while the seller supplies a counterfeit branded item — *"a consumer apparently buys a pen but actually has a counterfeit branded bag delivered."* Character plush is precisely the product family where this occurs, so brand-keyword filtering at ingestion is worth building in from the start.

---

## 6. Self-purchase and prohibited-activity rules

### 6.1 Buying through your own links

Clause 3.7.4 is unambiguous and covers households and colleagues:

> "engaging, directly or indirectly, or authorizing any dishonest or fraudulent transactions, whether by yourself or together with your associates (including but not limited friends, relatives, colleagues, and etc.), on the AliExpress Platform or the Site. Examples include, but are not limited to, you or your associates making purchases on the AliExpress Platform through designated links, or engaging in fraudulent behavior such as spam registration, hijacking and brand bidding with the intention that you or your associate may obtain unlawful or unjust commissions / bonuses / coupons and/or other forms of remuneration via the Program"

Clause 3.7.5 extends this to collaborating with AliExpress sellers for unjust commission, and cl. 3.7.10 to a seller joining the Programme to earn on their own sales. Employees, contractors and related companies are not named as such, but "associates … colleagues" plainly reaches them.

**Test purchases are not addressed.** No carve-out for testing, QA or screenshotting exists in any document. Given cl. 3.7.4 turns on intent ("with the intention that you or your associate may obtain unlawful or unjust commissions"), a genuine functional test is arguably different in kind from commission farming — but AliExpress has not said so, and a test order placed through your own tracking link will still generate a tracked, commissionable order. Ask support before doing it (§11).

### 6.2 The friends-and-family tension

Two live AliExpress texts pull in opposite directions, and both are recorded here rather than reconciled:

| Text | Source |
|---|---|
| *"You could also recommend AliExpress to friends and colleagues who might be interested in our products. You can share banners and links through email, Facebook, or any other social media."* | Help Centre, Top Question 8 / New User Guide 4 (live) |
| *"you or your associates making purchases on the AliExpress Platform through designated links … with the intention that you or your associate may obtain unlawful or unjust commissions"* is prohibited conduct | Rules cl. 3.7.4 (1 Aug 2025) |

The Rules and Policies are the newer, more specific and contractually senior document (Service Agreement cl. 2.1–2.2 makes the Rules an integral part of the agreement, and cl. 7.3 ranks them above the Agreement itself on commission matters). The distinction the two texts imply is between *recommending* to friends who buy because they want the product, and *arranging* purchases in order to generate commission. Existing customers of your own site are ordinary traffic and are not implicated.

### 6.3 What counts as artificial traffic

Consolidated from Rules cl. 3.6.3, 3.7.3, 3.7.6, 3.7.13 and 3.8, the Service Agreement's definition of Fraud (cl. 1.7), and the Help Centre's Types of Violation & Case Study:

- **Fraud** (Agreement cl. 1.7): *"any action that intentionally attempts to create sales, leads, or click-throughs using robots, frames, iframes, scripts, or manually 'refreshing' of pages, for the sole purpose of creating commissions."*
- **Cookie stuffing / dropping / hijacking**: any extension, add-on, plug-in, tool, bot or code that overlays, covers, alters or interacts with an AliExpress page, visible or not — including iframes, PPV pop-ups, notifications, pushes and floating screens.
- **Forced or induced clicks**: the case study describes a floating page where clicking to close it registers as a promotional click, and pages where "all traffic that has visited the page will lead to the calculation of commission".
- **Incentivised traffic**: cl. 3.7.3 prohibits "any form of resource exchange or barter as an advertising method", including "using any reward or incentive as a bait to induce visitors into performing certain actions". Note that cashback and loyalty-point channels are not banned — they are a recognised channel type with their own, lower, commission model.
- **Pop-up and pop-under advertising in any form** (cl. 3.7.6.2).
- **Traffic hijacking**: viruses, trojans, malicious plug-ins, bundled software, forced homepages, search-engine cheating, tampering with user information, modifying URL parameters.
- **Brand bidding** on Restricted Marks (cl. 3.7.7), and **in-app promotion** — posting affiliate links in AliExpress's own comment sections or channels (cl. 3.7.9).

Clause 3.8 lets AliExpress determine a violation from statistical signals alone: traffic source, channel, average conversion into clicks and purchases, and your performance relative to other participants. Clauses 3.5 and 3.6 each close with the same sentence, differing only in the clause reference: *"Participants shall not be entitled to any commission or payment for any and all orders and/or traffic generated in connection with the violation of this Clause 3.5 / 3.6."*

### 6.4 Account inactivity

Clause 6.2 lets AliExpress terminate an account that has been **inactive for 180 consecutive days** (not logged into, no services used), or that **has not published any advertisements or generated any Qualifying Purchases in the last 365 days**.

---

## 7. API permissions, authentication and exact quotas

### 7.1 Permissions on application 6102

| Permission group | Rule ID | Status | Auto-approved? |
|---|---|---|---|
| Standard API for Publishers ("Aliexpress Affiliates API (Default)") | 17286 | **Active** | — |
| System Tool | 7 | **Active** | yes (`autoAudit: 1`) |
| Advanced API | 17251 | **Inactive** | **no** — manual review |
| SKU Dimension API | 17502 | **Inactive** | **no** — manual review |

**What Advanced API unlocks** (Help Centre, Affiliate API Guidance §2.2 and the function table in §3):

- `aliexpress.affiliate.product.shipping.get` — **shipping/delivery information**
- `aliexpress.affiliate.product.sku.detail.get` — SKU-level detail
- `aliexpress.affiliate.product.smartmatch` — recommendation engine
- `aliexpress.affiliate.hotproduct.query` and `.download` — hot products (the high-commission pool)

Three of the five bear directly on this project: delivery information is on the requirements list, SKU detail drives variant selection for plush colours and sizes, and hot products are where the elevated commission rates live. **The application requirement is stated in the console itself:** *"Please provide your register email on portals when apply for this API."* SKU Dimension API is applied for the same way and covers the SKU Product Detail Info API.

### 7.2 Quotas — what the account actually shows

**No quota is published for this application.** Specifically:

- The App Overview page has an **"API Call Limit"** field and it is **blank**.
- The console's own record for the app carries `trafficRules: []`, `canApplyTraffic: false`, `nextTrafficRule: null`, `uprushPercent: null` — no traffic rule is configured, and none can currently be applied for.
- Data Dashboard > App Statistics offers Total Calls, Success/Fail, Success Rate and **Average QPS** columns, but they report usage, not a ceiling. For this app the result set is empty (no calls yet).

**Do not use the published QPS table as an affiliate quota.** The Open Platform documentation does contain a per-method QPS table (`aliexpress.ds.product.get` 500, `aliexpress.ds.feed.itemids.get` 3, and so on) — but it sits under **DropShippers API Developer → Quick Start**, lists only `aliexpress.ds.*` and `aliexpress.trade.*` methods, and covers none of the `aliexpress.affiliate.*` methods. Treating it as an affiliate quota would be exactly the inference the brief rules out.

**Functional limits that do apply** (Affiliate API Guidance §3 and §4.2, the Help Centre's tracking-link page, and the API reference):

| Limit | Value |
|---|---|
| Products per page | 50 |
| Pages per query | 100 |
| **Products retrievable per query** | **5,000** |
| Product IDs per `productdetail.get` call | 50 (Affiliate API Guidance §3) |
| Source links per `link.generate` call | 50 (API reference) |
| Tracking IDs per account | 50 (Help Centre, "How to generate tracking links?") — cannot be edited or deleted once added |

AliExpress publishes a documented workaround for the 5,000 ceiling: check `total_record_count`, and if it exceeds 5,000, narrow the query with `min_sale_price` / `max_sale_price` and walk the price bands upward until the whole catalogue is covered.

### 7.3 Authentication

**Account settings (App Console > Auth Management):**

- Authorized Policy: Allow login user to authorize
- **Access Token Duration: 30 days**
- **Refresh Token Duration: 60 days**
- Authorized Agreement: OAuth2.0 Server-side
- Authorized page: shown; no user limit set

The platform documentation adds that a **test-status** app gets only 1 day / 2 days, extended to 30/60 on going online; that refreshing resets the access-token clock **but not the refresh-token clock**; and that once the refresh token expires the user must re-authorise. (That passage sits in the DropShippers authentication section, so it is corroboration for platform mechanics, not an affiliate-specific rule.)

**Do affiliate calls need a token at all?** The evidence points to no, but AliExpress never states it outright:

- The API reference lists `access_token` in the common-parameter table of every affiliate method with **Required = No**.
- `app_signature` is documented as optional: *"It can be left blank. The app_signature is a non-mandatory parameter for the affiliate API."*
- AliExpress's own worked signature example for `aliexpress.affiliate.order.list` (Affiliate API Guidance §4.5) contains `app_key`, `start_time`, `end_time`, `status`, `method`, `timestamp` and `sign_method` — **and no `access_token`**.
- Endpoints: business methods go to `https://api-sg.aliexpress.com/sync?method={api_path}&{query}`; system (token) methods to `https://api-sg.aliexpress.com/rest{api_path}?{query}`.

This is recorded as an **inference from documented API capability**, not a confirmed rule. It is worth one support question before the ingestion layer is designed (§11), because the answer decides whether you need to build and maintain a token-refresh loop at all.

### 7.4 Rate-limit responses and retry behaviour

**Not documented for the affiliate APIs.** What exists:

- The platform-wide error taxonomy: **SYSTEM** (platform errors — *"check the authority, frequency and other conditions of the applications"*), **ISV** (business data errors), **ISP** (backend service errors — *"try again after some time"*).
- Documented error codes on the System Tool APIs include `ServiceUnavailable`, `IncompleteSignature`, `InvalidAppkey`, `InvalidCode`, `IllegalRefreshToken`, `isv.402`, `isv.insufficient-permission`, `isv.invalid-authorization`.
- Across the whole published API reference (200 methods in 18 categories), only one AE-Affiliate method documents error codes at all.
- The often-quoted *"Api access frequency exceeds the limit… It is recommended to sleep for 1–2 seconds and request again"* guidance is in the **DropShippers** FAQ against a 500-QPS connection limit, and does not govern affiliate methods.

Build the ingestion layer with exponential backoff and treat SYSTEM-class errors as retryable, but recognise that no published contract exists to code against.

### 7.5 Order reporting — push and pull

**Push (S2S).** Server-to-Server is AliExpress's affiliate order webhook: *"Once set up in Portals, S2S can push order messages to Affiliate participant in near real-time for paid orders."* Configured at **Portals > Tools > S2S Setting** (`/affiportals/web/tools/s2s_setting.htm`), where you supply a destination URL, choose fields, and add fixed parameters for channel differentiation. Delivery is a GET with your chosen parameter names, e.g. `http://yourdomain.example?OrderID=<order_id>&CommissionRate=<base_commission_rate>&URL=<url>&Channel=AliExpress`.

Documented S2S fields include `order_id` (sub-order ID), `item_id`, `effect_pay_time` (Pacific Time, "the time when the payment message is processed by the Affiliate program, which may be later than the actual payment time"), `platform`, `country` (ship-to), `order_amount`, `local_order_amout`, `currency`, `commission_rate`, `commission_fee`, `is_new_buyer`, `new_buyer_bonus`, `incentive_commission_rate`, `incentive_commission`, `af`/`cn`/`dp`/`cv`, `url` ("IP page where the order is attributed to the click"), `category`, `is_affiliate_item`, `is_hot_product`, `order_type` and `tracking_id`.

**The important limitation:** S2S fires **on payment**, not on receipt confirmation or settlement. Since commission only becomes real at Completed Settlement, you still need to poll `aliexpress.affiliate.order.list` to follow orders through the lifecycle. AliExpress documents exactly how: query by "Payment completed Time" across all four statuses to enumerate a month's orders, and by "Completed Settlement Time" with status "Completed Settlement" to find newly paid commission.

**Pull.** `aliexpress.affiliate.order.list` (by time range and status), `.listbyindex`, and `.get` (single or batch by order ID). Note the Live Order Tracking export retains only the **last 180 days** of order data, so monthly exports are advisable if you want a longer history.

**The App Console's Webhooks page is a different thing.** It offers subscription groups named Order Information, Product Information, Instant Messaging, System Notification and Logistics Information — the seller/dropshipper message service. For this Affiliates-category app no callback URL is set, nothing is subscribed, and expanding "Order Information" returned no child message types. Affiliate order push is configured in Portals, not there.

---

## 8. Product-data storage and automation rules

**Automated scheduled ingestion is permitted by design.** AliExpress describes the API as *"Channel-initiated Pull: API is initiated by the developer. The query fields are determined by the input parameters of the developer, and the developer can initiate queries at any time"*, positions it as the tool "to efficiently obtain product information, manage orders, and conduct batch link conversion", and publishes a step-by-step method for extracting an entire result set by price-banding. Nothing conditions this on manual operation or limits scheduling. There is, however, **no explicit clause granting permission to run scheduled jobs** — the permission is implied by the whole design of the product rather than stated.

**No storage, caching, retention or deletion rule exists for product data.** Searching the Service Agreement, the Rules and Policies and the Help Centre turns up no cache-duration limit, no obligation to refresh within N hours, no obligation to purge on delisting, and no data-retention ceiling. The constraints that do exist are adjacent, not about storage:

1. **Licence scope** — AliExpress Content is made available for advertising through the Programme (Rules cl. 1.4); it may not be transferred, copied, modified, adapted or made into derivative works without written consent (Service Agreement cl. 8.2), and icons, buttons, banners and graphics may not be copied or modified (cl. 8.4).
2. **Route of acquisition** — the Terms of Use ban systematic robot retrieval of site content to build a database without written permission (cl. 3.2(a)); the affiliate API is the authorised alternative.
3. **Personal Data** — Rules cl. 4.2 forbids collecting, using, storing, processing, disclosing or transferring **any Personal Data** in connection with the Programme without the data owner's written consent. This governs visitor data, not product records.
4. **Link decay** — Service Agreement cl. 5.4 lets AliExpress invalidate a tracking short-key that is over a year old, or under a year old with no clicks in six months, or suspected of pointing at infringing content. **An invalidated short-key stops tracking entirely.** For an automated catalogue this is the single most consequential operational rule in the document set: cached links need a regeneration policy well inside those windows.
5. **Report retention on AliExpress's side** — Live Order Tracking exports retain 180 days; violation records are kept for a maximum of 90 days.

---

## 9. Privacy and UK-specific requirements

### 9.1 What is passed to AliExpress on a click

**AliExpress does not enumerate this.** What it documents is what *you* can attach: the `tracking_id` embedded in the short key, plus optional `af`, `cn`, `cv` and `dp` values appended to the link. Those values are recorded by AliExpress and returned to you in the Live Order Tracking CSV and in S2S payloads. Beyond that, no AliExpress document lists the visitor data collected at the moment of the click.

There is a separate, opt-in path in which a publisher deliberately sends Personal Data to AliExpress — Service Agreement cl. 6.4 covers transferring device ID, ADID or user photos for content filtering, subject to prior approval, full user consent, and Schedule 2. That is not something this project needs to do.

### 9.2 Whose cookies

**AliExpress's.** *"We track completed transactions by using cookies"*, and the Cookie Notice (effective 16 May 2024) lists the first-party `aliexpress.com` cookies by name across Essential, Analytics and Personalisation/Advertising categories, stating that cookies are used for *"assisting our partners in tracking user visits to the Platforms"*. The tracking cookie is set on the AliExpress domain when the visitor lands there.

**AliExpress requires no cookie to be set by your site.** Whatever you set for your own local basket, analytics or preferences is yours alone and is not part of the affiliate mechanism.

### 9.3 What AliExpress asks of publishers

- **Rules cl. 4.1** — comply with all applicable Data Protection Laws at all times.
- **Rules cl. 4.2** — do not collect, use, store, process, disclose or transfer **any Personal Data** in connection with the Programme without the data owner's written consent; if you do, you do so "in your own capacity" and indemnify AliExpress.
- **Rules cl. 4.3** — nothing implies a controller–processor relationship between you and AliExpress.
- **Service Agreement cl. 6.2(b)(ii)** — content on your site must comply with laws governing unsolicited electronic commercial messages and the collection, storage, processing and usage of privacy data.

**No cookie-banner text, consent flow or privacy-policy wording is prescribed anywhere.** AliExpress states the compliance obligation and leaves the implementation entirely to the publisher.

### 9.4 Data-processing and international-transfer terms that name the UK

Schedule 2 of the Service Agreement is a Data Processing Addendum, and it is UK-aware:

- **cl. 1.2** — "Applicable Data Protection Law" expressly includes *"the United Kingdom Data Protection Act 2018"* alongside the GDPR and CCPA.
- **cl. 3.1** — *"the Parties acknowledge that Participant is a controller of the Data it discloses to AliExpress, and that AliExpress will process the Data as a separate and independent controller… In no event will the parties process the Data as joint controllers."*
- **cl. 4.1** — each party is individually responsible for its own controller obligations.
- **cl. 4.2** — neither party may transfer the data outside the EEA "unless it takes such measures as are necessary to ensure the transfer is in compliance with Applicable Data Protection Law". The measures named — "may include (without limitation)" — are an adequacy decision, binding corporate rules, or the **controller-to-controller Standard Contractual Clauses (EU) 2021/914**, which are incorporated by reference.
- **cl. 4.3** — mutual security-incident notification; **Rules cl. 7.3(b)** tightens this to notification **within 24 hours** for Personal Data received from AliExpress.

From the AliExpress Privacy Policy (effective 30 July 2026): for visitors based in the EEA and the UK, *"The data controller of your personal information is Alibaba.com Singapore E-Commerce Private Limited (incorporated in Singapore with Company Reg. No. 200720572D)"*, with international-transfer safeguards set out in Section N.

Contracting party: for users outside mainland China, the USA, South Korea and the "Relevant Jurisdictions", the counterparty is Alibaba.com Singapore E-Commerce Private Limited. The Affiliate Program Service Agreement is governed by **Hong Kong law** with **HKIAC arbitration** in Hong Kong, in English, before three arbitrators (cl. 14.3).

This section reports what AliExpress's documents say. It is not legal advice, and it does not address what UK law independently requires of a UK publisher.

---

## 10. Implications for the proposed Affiliate Superstore

**What works as designed.**

The catalogue model is squarely within what AliExpress invites — its own Help Centre proposes building "your own shopping guide", and "shopping > affiliate store" is a selectable channel type that this account already uses. A neutral `.co.uk` domain is the right call, and avoids the entire trademark minefield in cl. 3.1–3.4. Automated discovery, pricing and link generation through the API are the sanctioned route (as opposed to scraping, which the Terms of Use prohibit). GBP and English are supported: the product APIs accept `target_currency` including GBP and `target_language` including EN, and `ship_to_country` filters to UK-deliverable stock. And the attribution model is forgiving in the way that matters most for a catalogue: send the visitor for one plush, earn on whatever they actually buy.

**What needs a decision before build.**

*The basket is the biggest design constraint.* There is no way to hand a multi-item basket to AliExpress. A local list can hold twelve items, but the customer will make twelve separate journeys. Realistic options: present the local list as a shortlist or wish list rather than a basket; batch-generate its links in one `link.generate` call (up to 50); and consider whether a store-page or venue-page link is a better hand-off when several items come from one seller — since sub-orders settle per seller anyway.

*Get Advanced API and SKU Dimension API applied for early.* Delivery information and SKU-level detail are on the requirements list and neither is available today. Both need manual review, and the Advanced API description tells you exactly what to include: the Portals registration e-mail.

*Confirm the commission model.* The account currently renders under the Non-Transparent model (0% on non-affiliate products, no bonus) while My Websites says the site is verified and the model "will be adjusted". Re-check Portals > Payment > Commission Rules; if it still says Non-Transparent after the site verification has settled, raise it with support. Note also that the first site/URL declared sets the main Advertising Channel for the account — so if several themed shops are coming, think about which one is declared first.

*Plan for link decay.* Clause 5.4's one-year and six-month rules mean a cached link table needs a regeneration job. In a long-tail catalogue where most products get few clicks, the six-month no-clicks rule will bite far more often than the one-year rule.

*Model the economics honestly.* 7% on the product line only (no shipping, no VAT, no coupons), capped at USD 50, paid in USD by international bank transfer, with a USD 15 fee per withdrawal, roughly a month after the buyer confirms receipt — and receipt confirmation on cross-border plush orders is not quick. There is no New Buyer Bonus for UK traffic. The upside levers are Hot Products (link type 2, rates set per product and recorded at purchase time), the CPX 2.0 incentive programme, and the monthly Specific Product List.

*Build the order pipeline as push plus poll.* S2S gives near-real-time paid-order notifications; only polling tells you when an order actually settles or goes Invalid.

**What to keep away from.**

No "AliExpress", "AE" or "AliE" in the domain, subdomains, page titles, meta tags or any paid-search keyword. No framing or masking of AliExpress pages. No browser extension. No purchases through your own links by you, family, colleagues or contractors. No incentivising clicks with rewards. And in a plush and collectables niche specifically: filter aggressively for counterfeit character merchandise, because the Rules Against Promotion of Illegal Products carry permanent termination and forfeiture of settled funds, extended to any account AliExpress associates with yours by device, login or payment details.

---

## 11. Unanswered questions and where to resolve each

Every item below was searched for across the Affiliate Program Service Agreement, the Affiliate Program Rules and Policies (Parts A and B), the entire Portals Help Centre, the signed-in Portals account pages, the Open Platform documentation tree (72 pages) and the full API reference (200 methods), and is genuinely absent.

| # | Unanswered question | Where to resolve it | Suggested wording |
|---|---|---|---|
| 1 | **Cookie / attribution window duration** | Portals > **Feedback** button, or e-mail **affiliates@service.alibaba.com** | "What is the tracking cookie lifetime for the AliExpress Affiliate Program — how long after a click on an s.click.aliexpress.com link does a purchase still attribute to the publisher? Please confirm whether it differs between the AliExpress website, the mobile site and the AliExpress app." |
| 2 | **Attribution model** (last-click, first-click, other) and what happens when another affiliate's link is clicked afterwards | Same | "Is affiliate attribution last-click or first-click? If a buyer clicks publisher A's link and then publisher B's link before purchasing, which publisher is credited?" |
| 3 | **Does the tracking cookie survive hand-off into the AliExpress app**, and is there a documented app-link or deep-link scheme beyond the s.click short link? | Same, or Portals > Tools > Link Generator documentation request | "Does affiliate tracking persist when an s.click link opens the AliExpress mobile app rather than the mobile web site? Is there a documented deep-link/app-link format for affiliates?" |
| 4 | **Is a local basket or wish list on the publisher's own site permitted**, and is any wording or UI treatment required at hand-off? | Portals > Feedback | "We plan to let visitors build a shopping list on our own site and then send them to AliExpress via individual tracking links. Is that permitted, and is any specific wording or interface treatment required to make clear that checkout happens on AliExpress?" |
| 5 | **Is there any API or approved URL that transfers multiple products to an AliExpress basket in one operation?** | Portals > Feedback, or Open Platform support ticket at openservice.aliexpress.com/support/index.htm | "Is there any affiliate API or supported URL format that adds one or more products (or SKUs) to an AliExpress shopping cart in a single operation, with affiliate tracking preserved?" |
| 6 | **Are several themed shops under one neutral domain, separated by URL path, acceptable — and should each be declared separately in My Websites?** | Portals > Feedback | "We operate one neutral .co.uk domain with several themed shop sections under different URL paths. Should each section be declared as a separate site under Account > My Websites, or the domain once? Which declaration determines our Main Advertising Channel Type under Rules clause 5.3.1.3?" |
| 7 | **Why is this account showing the Non-Transparent Channels Commission Model** when the site is verified, and when will it change? | Portals > Feedback (reference Payment > Commission Rules) | "Our Commission Rules page shows the Non-Transparent Channels Commission Model although our site is declared and verified under Account > My Websites. Which model applies to us now, and when will the Default Channel model take effect?" |
| 8 | **Exact API quotas for app 6102** — daily calls, per-second rate, per-method limits | Open Platform support ticket at openservice.aliexpress.com/support/index.htm, quoting App ID 6102 | "Our App Overview shows a blank 'API Call Limit'. What are the daily and per-second call limits for the aliexpress.affiliate.* methods under the Standard API for Publishers permission group, and are there per-method limits?" |
| 9 | **Rate-limit error codes and required retry behaviour** for affiliate methods | Same ticket | "Which error code is returned when an aliexpress.affiliate.* call is throttled, and what backoff does AliExpress require?" |
| 10 | **Do affiliate product/link/order APIs require an OAuth access token**, or is a signed AppKey/AppSecret request sufficient? | Same ticket | "For the aliexpress.affiliate.* methods, is access_token required, or is a signed app_key request sufficient? Your Affiliate API Guidance signature example (section 4.5) omits access_token." |
| 11 | **Is automated scheduled catalogue ingestion expressly permitted**, and are there caching, retention or deletion rules for product data pulled from the API? | Same ticket, or Portals > Feedback | "Are we permitted to run scheduled jobs that pull the affiliate catalogue on a recurring basis and store the results? Are there any limits on how long we may cache product data, prices or images, or any obligation to refresh or delete them?" |
| 12 | **Are test purchases through your own links permitted** for functional testing? | Portals > Feedback | "Rules clause 3.7.4 prohibits purchases by us or our associates through our own links. Is a small number of genuine functional test orders permitted, and if so how should they be flagged so they are not treated as a violation?" |
| 13 | **Is bidding on seller trademarks (as opposed to AliExpress marks) prohibited?** | Portals > Feedback | "Clause 3.7.7 prohibits SEM on AliExpress Restricted Marks. Does the same prohibition extend to the trademarks of individual AliExpress sellers or of the brands whose products they sell?" |
| 14 | **Is any affiliate disclosure wording required by AliExpress?** | Portals > Feedback | "Does AliExpress require publishers to display a specific affiliate disclosure on their sites, and if so, is there prescribed wording?" |
| 15 | **Requirements on price display** (VAT, shipping) and on refreshing or removing stale prices, coupons, availability and delivery estimates | Portals > Feedback | "Are there requirements on how prices from the affiliate API must be displayed — for example whether they must include VAT or shipping — and any maximum age for displayed prices, coupons or delivery estimates?" |
| 16 | **Contents of the newest account notices** — "Notice of August Payment Update" (25 Aug 2026) and "Update Notice of AliExpress Affiliate Program Rules and Policies" (16 Jul 2025) | Portals > **notification bell** > Notifications > View details | The detail pane renders in a cross-origin frame that could not be read in this session. Open each notice directly in the browser; the August 2026 payment notice may supersede the withdrawal figures in §3.4. |

---

## 12. Source index

All pages were retrieved on **30 August 2026**. Archived copies are in `sources/` alongside this report. Each archived file carries a front-matter block with its title, source URL, effective date, retrieval date and capture method. No authenticated page containing credentials, personal information or financial detail has been archived; where an account page is cited, only the non-personal programme settings are reproduced.

### Contractual documents

| # | Title | URL | Effective date | Archived as |
|---|---|---|---|---|
| 1 | AliExpress Affiliate Program Service Agreement | https://terms.alicdn.com/legal-agreement/terms/suit_bu1_aliexpress/suit_bu1_aliexpress202003132026_84536.html | New version effective 31 March 2022 | `01-affiliate-program-service-agreement.md` |
| 2 | AliExpress Affiliate Program Rules and Policies (Parts A and B) | https://portals.aliexpress.com/affiportals/web/help_center.htm → Affiliate Program Rules → Affiliate Program Rules and Policies *(sign-in required)* | Part A updated 1 August 2025; Part B updated 1 April 2025 | `05-affiliate-program-rules-and-policies.md` |
| 3 | Commission rate tables, Clause 5.3 (published as images) | image URLs listed in the archived file | Part A, 1 August 2025 | `10-commission-rate-tables-transcribed.md` |
| 4 | AliExpress Affiliate Program Sub-Participant Management Rules | Help Centre → Affiliate Program Rules → Sub-Participant Management Rules *(sign-in required)* | 16 July 2024 | `06-helpcentre-sub-participant-management-rules.md` |
| 5 | Rules Against Promotion of Illegal Products | Help Centre → Affiliate Program Rules → Rules Against Promotion of Illegal Products *(sign-in required)* | 13 May 2025 (replaced "Combating Hidden Links in the AliExpress Affiliate Program Policy" of 1 January 2025) | `06-helpcentre-rules-against-illegal-products.md` |
| 6 | AliExpress.com Terms of Use | https://terms.alicdn.com/legal-agreement/terms/suit_bu1_aliexpress/suit_bu1_aliexpress202204182115_66077.html | Part A updated 26 August 2026, effective 26 September 2026 (page also carries the currently effective version) | `04-aliexpress-terms-of-use.md` |
| 7 | AliExpress.com Privacy Policy | https://terms.alicdn.com/legal-agreement/terms/suit_bu1_aliexpress/suit_bu1_aliexpress201909171350_82407.html | Effective 30 July 2026 | `02-aliexpress-privacy-policy.md` |
| 8 | AliExpress Cookie Notice | https://terms.alicdn.com/legal-agreement/terms/c_platform_service_agreement/20240401172006520/20240401172006520.html | Effective 16 May 2024 | `03-aliexpress-cookie-notice.md` |

### Portals Help Centre (sign-in required; all at https://portals.aliexpress.com/affiportals/web/help_center.htm)

| # | Page | Archived as |
|---|---|---|
| 9 | New User Guide | `06-helpcentre-new-user-guide.md` |
| 10 | Top Question | `06-helpcentre-top-question.md` |
| 11 | Glossary | `06-helpcentre-glossary.md` |
| 12 | How to generate tracking links? | `06-helpcentre-how-to-generate-tracking-links.md` |
| 13 | How to collect promotion materials? | `06-helpcentre-how-to-collect-promotion-materials.md` |
| 14 | Product Selection Instruction | `06-helpcentre-product-selection-instruction.md` |
| 15 | Insights about reports? | `06-helpcentre-insights-about-reports.md` |
| 16 | How to withdraw Portals deposits? | `06-helpcentre-how-to-withdraw-portals-deposits.md` |
| 17 | Types of Violation & Case Study | `06-helpcentre-types-of-violation-and-case-study.md` |
| 18 | Affiliate API Guidance | `06-helpcentre-affiliate-api-guidance.md` |
| 19 | S2S Guidance | `06-helpcentre-s2s-guidance.md` |

### Signed-in account pages (cited, selectively archived)

| # | Page | URL | Archived as |
|---|---|---|---|
| 20 | Portals → Payment → Commission Rules | https://portals.aliexpress.com/affiportals/web/commission_rules.htm | `07-portals-commission-rules-account-view.md` (account-holder name redacted) |
| 21 | Portals → Account → My Websites | https://portals.aliexpress.com/affiportals/web/my_websites_page.htm | **not archived** — contains e-mail, phone and payee fields. Non-personal facts cited in §3.1 and §5.3 only. |
| 22 | Portals → Message Center (notification titles and dates) | https://portals.aliexpress.com/affiportals/web/message_center.htm | not archived; titles and dates cited in §11 item 16 |
| 23 | Open Platform App Console, application 6102 | https://openservice.aliexpress.com/app/index.htm | `11-app-console-app-6102-observations.md` (AppKey, secret, e-mail omitted) |

### AliExpress Open Platform documentation

| # | Title | URL | Archived as |
|---|---|---|---|
| 24 | Affiliate Developers documentation branch (Notice, Quick Start, HTTP request, endpoints, calling parameters, signature algorithm, SDK) | https://openservice.aliexpress.com/doc/doc.htm | `08-openplatform-affiliate-developer-docs.md` |
| 25 | API Reference, AE-Affiliate category (cid 21407) — 16 methods with full parameter tables, error codes and response examples | https://openservice.aliexpress.com/doc/api.htm#/api?cid=21407 | `09-openplatform-affiliate-api-reference.md` |
| 26 | Full Open Platform documentation set (72 guide pages) and full API reference (200 methods across 18 categories), used to confirm the absence of affiliate quotas and to isolate DropShippers-only content | as above | delivered separately as `getting-started.md` and `api-reference.md` |

### Public Portals pages

| # | Page | URL | Archived as |
|---|---|---|---|
| 27 | AliExpress Affiliate Portals (public home) — "up to 9% basic commission rate (up to 90% for Hot Products)", "Earn up to 4% extra commission rate for promotions that surpass targets on our incentive programs", and the list of publisher archetypes AliExpress recruits | https://portals.aliexpress.com/ | `12-portals-public-home-page.md` |

### Checked and found not to exist

- `https://portals.aliexpress.com/help/help_center.html` and `/help.htm` — both redirect to the AliExpress sign-in page; the Insertion Order in the Service Agreement points at the first of these for commission rates and target countries.
- `https://sale.aliexpress.com/__pc/uTHnW6wRZg.htm` (an indexed copy of the Service Agreement) — returns 404.
- `https://common.aliexpress.com/…/adcms/affiliate/vip/io/agreement.htm` (an indexed Insertion Order page) — returns 404.
