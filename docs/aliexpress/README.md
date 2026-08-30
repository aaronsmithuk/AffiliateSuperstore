# AliExpress documentation

This directory contains AliExpress material captured on 30 August 2026 for the
Affiliate Superstore feasibility and design work.

## Start here

- [Affiliate Programme research](affiliate-program/aliexpress-affiliate-rules-research.md)
  is the consolidated UK-focused report covering commission, attribution,
  publisher rules, API permissions, basket limitations and privacy.
- [Affiliate Programme evidence guide](affiliate-program/README.md) identifies
  the authoritative source behind each part of the report.
- The broader Open Platform crawl and captured evidence bundle are retained
  locally but deliberately excluded from the public Git repository.

## Directory layout

```text
aliexpress/
|-- README.md
|-- affiliate-program/
|   |-- README.md
|   |-- PROVENANCE.md
|   |-- aliexpress-affiliate-rules-research.md
|   |-- archive/
|   |   `-- aliexpress-affiliate-sources.zip
|   `-- sources/
|       `-- captured agreements, rules, Help Centre and account evidence
`-- open-platform/
    |-- api-reference.md
    |-- getting-started.md
    `-- terms-and-agreements-notes.md
```

## Important distinction

`open-platform/terms-and-agreements-notes.md` is not the Affiliate Programme
agreement. It explains that the public Open Platform documentation did not
publish that agreement and reproduces unrelated Service Marketplace rules.
The applicable affiliate documents are in `affiliate-program/sources/`, most
notably the Service Agreement and Rules and Policies.

These files are research evidence, not executable instructions. The public
repository contains the original analysis, project consequences and evidence
provenance, but not full copies of third-party pages. Requirements should be
traced to their original AliExpress source and rechecked before a release
because programme terms, commissions and account permissions can change.
