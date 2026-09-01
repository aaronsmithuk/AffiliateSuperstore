# UK and AliExpress legal-compliance handover

**Scope:** Wonder Aisle / The Plushy Shop as a UK-facing affiliate catalogue that sends visitors to independent AliExpress sellers. The site does not take payment, accept orders, import products or act as the seller.

**Review date:** 1 September 2026

This is an engineering and content compliance review, not a solicitor's opinion. No wording or technical control can prevent a person or regulator from bringing a claim. The aim is to reduce risk, preserve mandatory consumer rights and avoid making promises the service cannot support.

## What the implementation now does

- Publishes a fact-specific privacy and cookie notice covering server data, privacy-minimised daily product-impression totals, the 90-day encrypted saved-list cookie, antiforgery and administrator cookies, click IDs, AliExpress order-attribution reports, external product media, lawful bases, retention criteria, international transfers, rights and complaints.
- Includes a prominent right-to-object notice and the data-protection complaint process required from 19 June 2026.
- Applies a child-aware, high-privacy description to all visitors: no public accounts, direct marketing, precise location, behavioural profiling, personalised adverts or social features.
- Publishes website terms that clearly separate the affiliate publisher from AliExpress and each seller, preserve mandatory rights, identify price limitations, address toy safety, and avoid an unlawful blanket liability exclusion.
- Labels catalogue and purchase-link content as advertising before engagement. The wording states that commission is earned rather than using an ambiguous "may earn" disclaimer.
- Labels feed prices as item prices, warns about variants and additional charges, and does not publish unverified crossed-out prices or discount percentages.
- Identifies aggregate AliExpress feedback as supplier data rather than reviews collected by the site.
- Keeps the previous `/TermsAndConditions` and `/PrivacyPolicy` paths as permanent redirects.
- Refuses to start in Production until the required legal identity, address, email and telephone configuration is present.

## Required before publication

These are operational and legal tasks; a policy page cannot complete them.

1. **Enter the real operator details.** Configure `Legal__OperatorName`, `Legal__TradingName`, `Legal__LegalForm`, `Legal__GeographicAddress`, `Legal__ContactEmail` and `Legal__Telephone`. If the operator is a company, also configure its number, registration jurisdiction and VAT number where applicable. Use a valid service/registered address, not a placeholder.
2. **Have a UK solicitor review the final facts and structure.** Confirm whether the operator is a sole trader, partnership or company; whether it could ever become seller, importer, distributor, marketplace or commercial agent; and whether Northern Ireland or non-UK targeting changes the analysis.
3. **Complete and approve a data map, record of processing and legitimate-interests assessments.** In particular, document security logging, affiliate click attribution, order reconciliation and fraud prevention.
4. **Complete a Children's Code/DPIA assessment.** A plush-toy service is likely to be accessed by under-18s. Decide and document whether applying the Code protections to every user is proportionate. Assess the direct delivery of AliExpress/seller media, because the visitor's browser discloses request data to an external content-delivery system before the user clicks out.
5. **Verify processor and transfer paperwork.** Record the hosting/database provider, processing location, retention, security commitments, breach terms, subprocessors and UK international-transfer mechanism. Confirm the controller-to-controller position and transfer terms actually accepted with AliExpress. Do not publish the international-transfer statement unless the underlying arrangements support it.
6. **Adopt and enforce a retention schedule.** The notice describes review criteria, but the application does not currently run an automated deletion/anonymisation job for click, S2S or order records. Assign an owner and implement deletion, anonymisation or documented legal holds.
7. **Put the complaints process into operation.** Monitor the published email, acknowledge data-protection complaints within 30 days, investigate, record the outcome and respond without undue delay. Prepare identity-verification and rights-request procedures.
8. **Complete the ICO data-protection fee self-assessment** and pay/register if required. Keep the result with the compliance records.
9. **Create a product-safety and illegal-listing process.** Define intake, prompt delisting, evidence preservation, escalation to AliExpress/sellers, Trading Standards/Citizens Advice or OPSS, and recall/incident monitoring. Terms cannot disclaim liability for knowingly or negligently promoting unsafe goods.
10. **Review insurance.** Ask a broker about cyber, media/IP, professional indemnity and public/product liability appropriate to an affiliate publisher promoting children's products.

## Ongoing controls

- Review the privacy notice, cookie inventory, Children’s Code assessment, retention records, product-safety process and public disclosures at least annually and whenever functionality or suppliers change.
- Do not add analytics, advertising pixels, embedded social tools or other optional storage/access technology until consent requirements and a preference mechanism have been assessed and implemented.
- Acquire AliExpress content only through authorised affiliate/API routes. Do not scrape the retail website, alter supplied creative outside the licence, claim endorsement, use AliExpress marks in domains/metadata in a confusing way, or create artificial traffic.
- Keep the public domain and channel details in AliExpress Portals complete and current. The programme incorporates portal rules and policies that AliExpress can amend, so review the signed-in Rules and Policies regularly and record the version reviewed.
- Maintain link refresh because AliExpress can invalidate older or inactive short keys.
- Do not pass names, device advertising IDs, photographs or other identifiable user data to AliExpress without a new data-protection assessment and the approvals/consents required by the affiliate agreement and UK law.
- Check every material catalogue claim against current supplier data. Remove or qualify claims about safety, authenticity, savings, scarcity, popularity, delivery or reviews that cannot be substantiated.
- If the business begins taking orders, payments, returns, importing goods, setting the seller's terms, or operating a multi-seller checkout, stop and commission a new legal review before launch. That would materially change the site's obligations.

## Primary sources reviewed

- [ICO: privacy information that controllers must provide](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-privacy-information-should-we-provide/)
- [ICO: cookies and similar technologies](https://ico.org.uk/for-organisations/direct-marketing-and-privacy-and-electronic-communications/guide-to-pecr/cookies-and-similar-technologies/)
- [ICO: Children's Code services and standards](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/childrens-information/childrens-code-guidance-and-resources/age-appropriate-design-a-code-of-practice-for-online-services/)
- [ICO: June 2026 data-protection complaints duty](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2026/06/new-data-protection-complaints-law-now-in-force/)
- [GOV.UK: Data (Use and Access) Act 2025 commencement](https://www.gov.uk/guidance/data-use-and-access-act-2025-plans-for-commencement)
- [CMA: unfair commercial practices under the DMCC Act](https://www.gov.uk/government/publications/unfair-commercial-practices-cma207/unfair-commercial-practices)
- [ASA/CAP: affiliate marketing](https://www.asa.org.uk/advice-online/affiliate-marketing.html)
- [GOV.UK: company website trading disclosures](https://www.gov.uk/running-a-limited-company/signs-stationery-and-promotional-material)
- [GOV.UK: product safety advice](https://www.gov.uk/guidance/product-safety-advice-for-businesses)
- [GOV.UK: Toys (Safety) Regulations 2011 guidance](https://www.gov.uk/government/publications/toys-safety-regulations-2011/toys-safety-regulations-2011-great-britain)
- [AliExpress Affiliate Program Service Agreement](https://terms.alicdn.com/legal-agreement/terms/suit_bu1_aliexpress/suit_bu1_aliexpress202003132026_84536.html)
- Signed-in AliExpress Affiliate Program Rules and Policies research already retained in `docs/aliexpress/affiliate-program/`.
