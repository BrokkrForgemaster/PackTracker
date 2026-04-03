# Data Source Strategy

## Goal
Define how PackTracker should obtain, store, refresh, and trust blueprint/material/mining-related data without becoming operationally dependent on third-party websites.

---

## Core Recommendation
PackTracker should maintain its own **internal normalized data model** for:
- blueprints
- recipes
- materials
- material acquisition sources
- org ownership records
- crafting/procurement workflows

Third-party websites may be used as:
- research references
- initial dataset seed sources
- periodic validation references

They should **not** be the long-term runtime dependency for PackTracker core functionality.

---

## Reference Sources Identified
The following applications were named as research references:
- SC Mission Database (`scmdb.net`)
- SC Crafter (`sccrafter.com`)
- Rockbreaker (`rockbreaker.peacefroggaming.com`)

Potential value from those sites:
- blueprint/item listings
- recipe/material requirements
- acquisition guidance
- mining/material information

Potential risks:
- schema changes
- rate limits
- no public API / unstable scraping targets
- legal or terms-of-service uncertainty
- data disappearing or becoming stale
- operational dependency on services outside House Wolf control

---

## Source Strategy Options

## Option A — Live third-party dependency
PackTracker queries external sites/APIs directly at runtime.

### Pros
- fast to prototype
- no local data maintenance initially

### Cons
- fragile
- difficult to guarantee availability
- difficult to cache/version correctly
- legal/operational dependence on third-party services
- poor fit for org-owned operations tooling

### Recommendation
Do not use this as the primary production model.

---

## Option B — Imported snapshot model
PackTracker imports curated datasets from one or more external references into its own internal schema.

### Pros
- PackTracker owns the operational dataset
- UI and workflows continue working if sources go offline
- easier to version and validate data
- easier to add org-specific metadata
- safer for internal workflows

### Cons
- requires import pipeline
- requires curation and update process

### Recommendation
This is the recommended model.

---

## Option C — Fully manual internal curation
House Wolf manually builds and maintains the dataset in PackTracker.

### Pros
- maximum control
- no outside dependency
- highly tailored to org operations

### Cons
- high maintenance burden
- slow to build completeness
- risk of stale or incomplete records

### Recommendation
Use only for:
- corrections
- verification
- org-specific notes
- special-case content

Not ideal as the sole source for all game data.

---

## Recommended Hybrid Strategy
Use a hybrid model:

### 1. Internal canonical schema
PackTracker stores all runtime data in internal entities.

### 2. External source ingestion
Use external references only to seed and periodically refresh the canonical dataset.

### 3. Human curation layer
Allow officers/admins to correct, annotate, verify, and retire records.

### 4. Versioned provenance
Each imported record should store:
- source name
- source URL or origin identifier where practical
- imported at timestamp
- game version/build when known
- confidence level

---

## Data Trust Model
Each major record should support trust metadata.

Suggested fields:
- `SourceName`
- `SourceVersion`
- `ImportedAt`
- `VerifiedAt`
- `VerifiedBy`
- `Confidence`
- `Notes`

Confidence examples:
- `Imported`
- `Verified`
- `NeedsReview`
- `Deprecated`

This is important because Star Citizen game systems evolve and community data can drift.

---

## Internal Dataset Requirements
PackTracker should support these internal data collections:

### Blueprint catalog
- canonical record for each in-game blueprint

### Recipe catalog
- blueprint to output mapping
- ingredient list
- craft quantity
- station requirements where known

### Material catalog
- raw ore
- refined material
- manufactured component
- subcomponent

### Material source catalog
- mined
- purchased
- salvaged
- mission reward
- crafted
- unknown / needs review

### Org capability catalog
- who owns blueprint
- who is verified
- who is available to fulfill

### Operational ledgers
- crafting requests
- procurement/mining requests
- stockpile records

---

## Ingestion Strategy

## Phase 1 — Manual seed import
Short-term implementation:
- create importable JSON/CSV seed files
- import into PackTracker DB through admin tooling or seed service
- manually review critical blueprint/material records

Benefits:
- fastest safe route
- avoids direct live scraping dependency
- allows early MVP without waiting for full automation

## Phase 2 — Structured importer
Build an importer that maps source data into PackTracker’s canonical schema.

Capabilities:
- parse source exports or curated data files
- detect duplicates
- update changed records
- preserve record provenance
- flag removed/retired records instead of hard deleting

## Phase 3 — Scheduled refresh pipeline
If a stable/legal source is available, add periodic refresh.

Requirements:
- rate limiting
- source health checks
- diffing before overwrite
- confidence downgrade when records disappear or conflict

---

## Anti-Dependency Rules
To avoid PackTracker becoming brittle:

1. Do not block core UI on live third-party calls.
2. Do not require external websites for request fulfillment workflows.
3. Do not store only remote URLs without normalized local records.
4. Do not let scraping shape the product model.
5. Always preserve a last-known-good internal dataset.

---

## Mining Data Strategy
Mining-related data should follow the same model.

Use internal normalized entities for:
- ore/material names
- known acquisition environments
- known conversion/refinement relationships
- mappings between mined ore and crafting materials

Even if external mining references are consulted, PackTracker should ultimately store:
- the ore/material mapping internally
- any org-specific mining notes internally

This is critical for generating miner-facing requests from crafting shortages.

---

## Legal/Operational Considerations
Before any automated scrape/import pipeline is treated as production-ready, review:
- source terms of use
- rate limits
- attribution requirements
- permission requirements if any

Preferred production posture:
- import snapshots or curated datasets
- avoid aggressive scraping
- keep operational tooling independent of external uptime

---

## Recommendation Summary

### Production recommendation
- PackTracker owns the canonical data model.
- External sources are used for research, seeding, and refresh — not as the live runtime dependency.
- Human curation and verification remain part of the workflow.

### Implementation recommendation
1. build canonical schema first
2. support manual/seed import next
3. add curation/admin editing
4. add structured importer later
5. add scheduled refresh last

This approach gives House Wolf:
- resilience
- operational control
- org-specific flexibility
- lower dependency risk
