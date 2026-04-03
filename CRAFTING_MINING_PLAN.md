# Crafting & Mining Intelligence Plan

## Purpose
This document defines the product and architecture plan for expanding PackTracker into a House Wolf operations platform for:
- blueprint discovery
- blueprint ownership visibility
- material requirement tracking
- mining/material procurement coordination
- crafting request fulfillment
- org-level logistics awareness

The objective is to design the system properly before implementation so development can proceed in structured phases with a clear data model and workflow map.

---

## Strategic Goal
PackTracker should become the internal operations platform House Wolf members use to answer these questions quickly:

- Is this blueprint available in-game?
- Where is the blueprint obtained?
- What materials are required to craft the item?
- Which members already own this blueprint?
- Can I request that another member craft this item for me?
- What materials are missing for the request?
- Can those materials be requested from miners or logistics crews?
- What does the org currently have on hand versus what must be gathered?

This turns PackTracker from a general utility shell into an org workflow system with strong game-specific operational value.

---

## Product Vision

### Core vision
Provide a single PackTracker workflow where a member can:
1. search for a craftable item or blueprint
2. inspect required materials and acquisition source
3. see whether the org already has a blueprint owner
4. submit a crafting request
5. automatically generate supporting miner/material requests if ingredients are missing
6. track fulfillment through completion

### Design principles
- Internal source of truth over dependency on live third-party websites
- Org workflow first, public wiki second
- Search and action in the same surface
- Blueprint ownership and material availability treated as org resources
- Mining and crafting support linked, not isolated

---

## Primary User Roles

### 1. Member / Requester
Can:
- search blueprints and craftable items
- view recipes and acquisition info
- request a crafted item
- request raw/intermediate materials
- see request status

### 2. Crafter / Blueprint Owner
Can:
- register owned blueprints
- mark themselves available/unavailable for crafting
- accept or decline crafting requests
- track required inputs
- mark request fulfilled

### 3. Miner / Material Supplier
Can:
- view ore/material requests
- claim procurement requests
- log delivered quantities
- indicate whether material is raw or refined

### 4. Logistics / Quartermaster / Officer
Can:
- verify blueprint ownership
- manage org stockpile
- route requests to crafters/miners
- resolve disputes and duplicates
- maintain curated blueprint/material records

### 5. Admin / Data Maintainer
Can:
- import or curate blueprint datasets
- validate source/acquisition info
- retire obsolete recipes
- approve data changes

---

## Problem Areas to Solve

### A. Blueprint intelligence
Members need a reliable way to know:
- whether a blueprint exists in-game
- what it produces
- where it comes from
- what it requires

### B. Ownership visibility
Members need to know:
- who owns a blueprint
- whether ownership is verified
- whether the owner is currently available to fulfill a request

### C. Material planning
Members need to know:
- required raw materials
- required refined/intermediate materials
- shortages against stockpile
- whether missing materials can be sourced internally

### D. Cross-team fulfillment
PackTracker must support the chain:
- blueprint search
- crafting request
- material shortage detection
- mining/material procurement request
- delivery confirmation
- final crafting completion

---

## Functional Scope

## 1. Blueprint Search & Discovery
Capabilities:
- search by blueprint name
- search by crafted item name
- filter by item category
- filter to in-game obtainable only
- show acquisition method/location/source
- show version/build metadata when known

Display fields:
- blueprint name
- crafted item name
- category/type
- in-game obtainable flag
- acquisition source
- acquisition location/vendor/mission where known
- game build/source confidence
- notes

## 2. Recipe / Material Breakdown
Capabilities:
- show ingredient list for a blueprint
- show quantities required
- distinguish raw ore vs refined material vs manufactured component
- show dependency chains if a component itself is craftable
- calculate aggregate totals for nested recipes in later phases

Display fields:
- material/component name
- amount needed
- unit type
- tier/category
- acquisition type (mined, purchased, salvaged, crafted, mission reward)

## 3. Blueprint Ownership Registry
Capabilities:
- member marks blueprint as owned
- officer/quartermaster may verify ownership
- optional proof metadata
- track owner availability state
- track crafting specialization notes

States:
- claimed
- verified
- inactive
- unavailable

## 4. Crafting Request Workflow
Capabilities:
- create a request tied to a blueprint
- specify quantity and urgency
- attach intended recipient/requester
- route to available blueprint owners
- claim, assign, complete, cancel
- show missing materials before acceptance if stockpile is insufficient

Fields:
- blueprint requested
- quantity
- priority
- requester
- assigned crafter
- required materials snapshot
- stockpile delta
- delivery location
- reward / reimbursement
- status timeline

## 5. Mining / Material Procurement Requests
This is a first-class feature and not optional.

Capabilities:
- create miner-facing requests for ore/materials needed by crafting
- allow manual or auto-generated requests from a crafting shortage
- distinguish raw ore requests from refined material requests
- support multiple helpers or crew size
- route to miners/logistics members

Fields:
- requested ore/material
- requested quantity
- raw vs refined preference
- target craft request / blueprint linkage
- priority
- delivery/drop-off location
- deadline
- reward offered
- number of helpers needed
- assigned miner(s)
- status
- delivered quantity

## 6. Org Inventory / Stockpile Awareness
Capabilities:
- track what the org has on hand
- compare stockpile against required materials
- reserve stock for active crafting jobs
- identify net shortage

Phase 1 stockpile can be manual.
Later phases can add structured inventory tracking and audit trails.

## 7. Search Across Org Capabilities
Members should be able to ask questions like:
- who can craft X?
- who owns blueprint Y?
- what is needed to make Z?
- what ore should miners collect for current requests?
- what requests are blocked on missing material?

---

## Recommended Product Surfaces

## Surface A — Blueprint Explorer
Purpose:
- browse/search blueprints and recipes

Primary views:
- search results
- blueprint detail
- recipe/material breakdown
- acquisition/source detail
- ownership panel

## Surface B — Crafting Operations
Purpose:
- manage crafting requests and fulfillment

Primary views:
- active crafting requests
- by-status queue
- assigned-to-me view
- blocked-by-material view

## Surface C — Mining Support / Material Acquisition
Purpose:
- route missing material needs to miners/logistics

Primary views:
- open mining requests
- grouped by ore/material
- linked crafting shortage view
- mine/deliver/complete workflow

## Surface D — Org Capability Registry
Purpose:
- know who owns what and what can be fulfilled internally

Primary views:
- member blueprint ownership
- verified blueprint owners
- availability/role filter

---

## Domain Model Recommendation

## Core entities

### Blueprint
Represents an in-game blueprint.
Suggested fields:
- Id
- Slug
- BlueprintName
- CraftedItemName
- Category
- Description
- IsInGameAvailable
- AcquisitionSummary
- AcquisitionLocation
- AcquisitionMethod
- SourceVersion
- DataConfidence
- Notes
- CreatedAt
- UpdatedAt

### BlueprintRecipe
Represents the recipe header for a blueprint.
Suggested fields:
- Id
- BlueprintId
- OutputQuantity
- CraftingStationType
- TimeToCraft
- Notes

### BlueprintRecipeMaterial
Represents each required material/component.
Suggested fields:
- Id
- BlueprintRecipeId
- MaterialId
- QuantityRequired
- Unit
- IsOptional
- IsIntermediateCraftable
- Notes

### Material
Represents a material/component/ore.
Suggested fields:
- Id
- Name
- Slug
- MaterialType
- Tier
- SourceType
- IsRawOre
- IsRefinedMaterial
- IsCraftedComponent
- Notes

### MaterialSource
Represents how a material is obtained.
Suggested fields:
- Id
- MaterialId
- SourceMethod
- Location
- Notes
- SourceVersion
- Confidence

### MemberBlueprintOwnership
Represents blueprint ownership by org members.
Suggested fields:
- Id
- BlueprintId
- MemberProfileId
- OwnershipStatus
- VerifiedByProfileId
- VerifiedAt
- AvailabilityStatus
- Notes

### CraftingRequest
Represents a request to craft an item from a blueprint.
Suggested fields:
- Id
- BlueprintId
- RequesterProfileId
- AssignedCrafterProfileId
- QuantityRequested
- Priority
- Status
- DeliveryLocation
- RewardOffered
- RequiredBy
- Notes
- CreatedAt
- UpdatedAt
- CompletedAt

### MaterialProcurementRequest
Represents a miner/logistics-facing request for materials.
Suggested fields:
- Id
- LinkedCraftingRequestId
- MaterialId
- QuantityRequested
- QuantityDelivered
- PreferredForm
- Priority
- DeliveryLocation
- AssignedToProfileId
- NumberOfHelpersNeeded
- RewardOffered
- Status
- Notes
- CreatedAt
- UpdatedAt
- CompletedAt

### OrgInventoryItem
Represents stockpile state.
Suggested fields:
- Id
- MaterialId
- QuantityOnHand
- QuantityReserved
- StorageLocation
- UpdatedAt

---

## Relationship Model
- Blueprint has one or more BlueprintRecipe records
- BlueprintRecipe has many BlueprintRecipeMaterial records
- Material may appear in many recipes
- Blueprint may have many MemberBlueprintOwnership records
- CraftingRequest references a Blueprint
- CraftingRequest may spawn one or more MaterialProcurementRequest records
- OrgInventoryItem offsets CraftingRequest shortages

---

## Workflow Design

## Workflow 1 — Search and inspect
1. Member searches blueprint/item
2. System returns in-game obtainable results only by default
3. Member opens detail page
4. System shows recipe, acquisition source, and org ownership

## Workflow 2 — Request crafting
1. Member opens blueprint detail
2. Selects quantity
3. Clicks request crafting
4. System snapshots recipe requirements
5. System compares stockpile and known shortages
6. Request enters crafting queue

## Workflow 3 — Generate mining/material requests
1. Crafting request is created
2. System detects missing materials
3. User or officer chooses generate procurement requests
4. System creates one or more miner/logistics requests
5. Miners claim and fulfill those requests

## Workflow 4 — Fulfill crafting request
1. Crafter accepts request
2. Required materials become reserved
3. Missing items remain linked as blockers
4. When all requirements are satisfied, request moves to in-progress
5. Crafter marks completed

## Workflow 5 — Register blueprint ownership
1. Member selects blueprint
2. Marks as owned
3. Officer optionally verifies
4. Availability status can be toggled

---

## MVP Recommendation

## MVP Objective
Deliver an operational first version for House Wolf that answers:
- what is needed to craft an item?
- who can craft it?
- how do I request it?
- what materials must miners gather to support it?

## MVP Scope
- blueprint search
- blueprint detail with recipe and acquisition source
- member blueprint ownership registration
- crafting request creation and status tracking
- material shortage detection using manual stockpile values or no-stockpile fallback
- miner/material procurement requests
- basic admin/curation tools for blueprint/material records

## Not required for MVP
- automatic live scraping from third-party websites
- full recursive recipe trees
- advanced analytics
- mobile client
- full auto-updating stockpile ingestion

---

## Rollout Plan

## Phase 1 — Data foundation + explorer
- blueprint entities
- material entities
- recipe entities
- search + detail UI
- admin/curation import path

## Phase 2 — Ownership + crafting requests
- ownership registry
- crafting request workflow
- assigned crafter logic
- request statuses

## Phase 3 — Mining/material requests
- shortage detection
- miner request creation
- procurement workflow
- linkage to crafting requests

## Phase 4 — Inventory and fulfillment intelligence
- org stockpile
- reservation logic
- blocked/unblocked state
- fulfillment dashboards

## Phase 5 — Automation and optimization
- dataset import tooling
- scheduled update pipeline
- analytics for high-demand materials/blueprints
- availability optimization

---

## UI/UX Recommendations
- Add a top-level navigation surface for `Crafting Ops`
- Keep search-first design
- Use detail panes/cards for blueprint breakdowns
- Reuse request UI patterns for crafting/mining requests
- Clearly label whether data is verified, inferred, imported, or stale
- Show shortages visually
- Treat org ownership as a capability map

---

## Risks

### Data volatility
Star Citizen systems change over time.
Mitigation:
- track source version/build
- track confidence
- treat all imported data as versioned records

### Third-party dependency risk
Community sites may change, disappear, rate-limit, or prohibit scraping.
Mitigation:
- maintain internal normalized dataset
- prefer import/snapshot model over live dependency

### Verification burden
Member-reported ownership may be inaccurate.
Mitigation:
- support verification state
- distinguish claimed from verified ownership

### Scope explosion
Blueprints, materials, inventory, mining, crafting, and requests can sprawl quickly.
Mitigation:
- enforce phased rollout
- ship MVP before advanced automation

---

## Recommendation
Proceed with implementation in this order:
1. internal data model for blueprints/materials
2. blueprint explorer UI/API
3. ownership registry
4. crafting requests
5. miner/material procurement requests
6. inventory/stockpile support

This creates a coherent operational system instead of isolated features.
