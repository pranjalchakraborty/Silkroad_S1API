# NPC Custom Buyers & Dealers Expansion Mod for Schedule 1

Welcome to the NPC Custom Buyers & Dealers Expansion Mod! This mod allows players and content creators to dynamically expand their in-game drug empire by introducing interlinked, customizable Non-Player Characters (NPCs) who act as buyers or dealers.

**Game:** Schedule 1  
**Primary Audience:** Players looking for an enhanced experience and Modders wishing to create or customize NPC content.  
**Core Dependency:** This mod requires `s1api` (another mod that acts as an API for the game) to function.

## Table of Contents

1.  [Core Features](#core-features)
2.  [How It Works (Gameplay Mechanics)](#how-it-works-gameplay-mechanics)
    * [NPCs & Progression](#npcs--progression)
    * [Quests](#quests)
    * [Orders & Deliveries](#orders--deliveries)
    * [Effects (Necessary & Optional)](#effects-necessary--optional)
    * [Rewards](#rewards)
    * [Dialogues](#dialogues)
3.  [Installation & Setup](#installation--setup)
4.  [Creating & Customizing NPCs (JSON Guide)](#creating--customizing-npcs-json-guide)
    * [Overview](#overview)
    * [Key JSON Concepts & Fields](#key-json-concepts--fields)
    * [Important JSON Rules & Assumptions](#important-json-rules--assumptions)
5.  [JSON Editor Tool](#json-editor-tool)
6.  [Current Development Status](#current-development-status)
    * [Pending Core Features](#pending-core-features)
    * [Current Gameplay Notes & Limitations](#current-gameplay-notes--limitations)
7.  [Roadmap & Future Ideas](#roadmap--future-ideas)
    * [Ongoing Development Tasks](#ongoing-development-tasks)
    * [Planned In-Game UI Enhancements](#planned-in-game-ui-enhancements)
    * [Future Concepts & Possibilities](#future-concepts--possibilities)
8.  [For Developers & Content Creators](#for-developers--content-creators)
9.  [Checklist to Release](#checklist-to-release)

## Core Features

* **Customizable NPCs:** Create unique buyers and dealers, each with their own characteristics. NPCs are defined via JSON files, allowing for deep customization.
* **Independent Reputation:** Each NPC maintains their own reputation level with the player, influencing interactions and available opportunities.
* **Unlockable Content:** As reputation with an NPC grows, players can unlock:
    * Increased order amounts.
    * New preferred product effects.
    * Access to entirely new NPCs.
* **Dynamic Quests:**
    * Engage in one delivery quest at a time.
    * Quests are generated for each NPC, covering various drug types upon quest refresh.
* **Flexible Deliveries:** Deliver orders in parts. However, rewards are only granted upon full completion of the order within the given timeframe.
* **JSON-Driven:** The entire NPC ecosystem is driven by JSON files, offering extensive control to modders.
* **JSON Editor Tool:** A companion tool is provided to simplify the creation, merging, and editing of NPC JSON files.
* **Starter NPC Pack:** Includes initial NPC configurations based on characters from "Breaking Bad" to get you started.

## How It Works (Gameplay Mechanics)

### NPCs & Progression

* **Reputation System:** NPC reputation is minimum at `1` and can increase indefinitely. Higher reputation unlocks more benefits.
* **NPC Unlocks:** New NPCs can be set to unlock once the player reaches specific reputation milestones with other, prerequisite NPCs.

### Quests

* **One Quest at a Time:** Players can only have one active delivery quest from this mod at any given moment. Complete or fail the current quest to take on another.
* **Quest Generation:** Upon quest refresh, the mod generates one quest for each available NPC for each drug type they deal in.
* **Product Variety:** If multiple JSON definitions exist for the same product type, one will be chosen randomly for the quest.
* **Quality Tier:** NPCs will only order products up to the maximum quality tier unlocked by the player's current reputation with them.

### Orders & Deliveries

* **Dynamic Order Amounts:** The minimum and maximum quantity of products an NPC orders scales with the player's reputation. This scaling uses a logarithmic function: `(log_base(Reputation) + 1) * base_amount`. The `base` for the logarithm (`repLogBase`) is configurable per NPC in the JSON; a base of `0` means no logarithmic scaling is applied.
* **Partial Deliveries:** You can deliver products for a quest in multiple installments.
* **Full Completion for Rewards:** All rewards (money, reputation, XP) are only granted once the entire order is fulfilled before the quest timer expires.

### Effects (Necessary & Optional)

NPCs can have preferences for certain product effects, which can be necessary or optional. The likelihood of an effect being part of a quest is determined by a value in the NPC's JSON definition:

* **Optional Effects:** If an effect has a JSON probability value in the range `[0, 1)` (e.g., `0.0` to `0.999...`), it's considered optional. The value itself represents the chance (e.g., `0.7` = 70% chance) that this effect will be requested as an optional component.
* **Necessary Effects:** If an effect has a JSON probability value in the range `(1, 2]` (e.g., `1.000...1` to `2.0`), it's considered necessary. The probability is calculated as `(JSON value - 1)`. For example, a JSON value of `1.7` means there's a `0.7` (or 70%) chance this effect will be a required component of the order.

    * **Warning:** Be mindful when creating JSONs, as randomly chosen *necessary* effects might sometimes be impossible to achieve with the game's current mixing logic.

### Rewards

Successful deliveries yield various rewards, calculated as follows:

* **Money Reward:**
    `money = (base_dollar*random4) + (total_price_of_delivered_products * (1 + sum_of_all_effects_(dollar_mult*random1)) * (1 + quality_dollar_mult) * dealTimesMult)`
* **Reputation Reward:**
    `rep = base_rep*random2 + (money_reward * rep_mult*random2)`
* **XP Reward:**
    `xp = base_xp*random3 + (money_reward * xp_mult*random3)`
    * **Note:** XP rewards are currently unsupported by `s1api` and will not be granted in-game until API support is added.

Fields like `base_dollar`, `base_rep`, `base_xp`, and the various `_mult` values are configurable per NPC in their JSON definition.

### Dialogues

NPC dialogues can be customized and support dynamic placeholders that will be replaced with quest-specific information in-game:

* `{product}`: The name of the product requested.
* `{amount}`: The *remaining* amount of the product needed (updates after each partial delivery).
* `{quality}`: The requested quality of the product.
* `{effects}`: The list of necessary effects.
* `{optionalEffects}`: The list of optional effects.

Standard dialogue event types include:
* `intro`: First message when interacting with the NPC.
* `dealStart`: Message detailing the new delivery quest.
* `incomplete`: Message after a partial delivery when more is still needed.
* `fail`: Message if the quest is cancelled.
* `expire`: Message if the quest timer runs out.
* `success`: Message when all products for the deal are successfully delivered.
* `reward`: Message when rewards are processed (can have a delay after success).

## Installation & Setup

*(Standard mod installation instructions for Schedule 1 should be followed. If you are unsure, please refer to general Schedule 1 modding guides.)*

1.  **Install Dependencies:** Ensure you have the `s1api` mod installed and enabled, as this mod relies on it.
2.  **Install This Mod:** Place the mod files into the appropriate directory for Schedule 1 mods.

### Important: Before Updating This Mod

To prevent issues when updating to a newer version of this mod:

1.  **Complete Ongoing Quests:** Finish or cancel any active delivery quests from this mod. Then Save. 
2.  **Backup NPC Data:** It's highly recommended to backup this mod's NPC-specific data from your save file. (Specify path or method if known, otherwise general advice). Backup whole save file and if later you find any mod NPC lost data, you can copy paste his folder from backup to new save.

## Creating & Customizing NPCs (JSON Guide)

### Overview

This mod uses JSON (JavaScript Object Notation) files to define all aspects of NPCs, their quests, and progression. You can create your own NPCs or modify existing ones by editing these JSON files.

* **Manual Editing:** Use any text editor (like Notepad++, VS Code, etc.) to open the JSON and edit manually or with AI help.
* **JSON Editor Tool:** A dedicated JSON Editor tool is provided with this mod to help you easily merge, view, and edit NPC JSON configurations.

**NOTE:** Using Notepad++ to check the JSON is recommended at least once, or in combination with using the tool. Some fields at the top are used in code but not NPC specific and not shown in JSON Editor currently.

### Key JSON Concepts & Fields

Below are some of the crucial fields and structures you'll encounter in the NPC JSON files:

* **NPC Object:** Each NPC is typically an object within a larger JSON array.
* **`reputation` (Initial):** While reputation is tracked dynamically in-game, NPCs start at `0`.
* **`unlockRep`:** An integer value specifying the reputation level required with *this* NPC to unlock:
    * Specific drug types they will order.
    * Higher qualities of existing drugs.
    * Specific preferred effects (both necessary and optional).
* **`unlockRequirements`:** An array of objects defining prerequisite NPC reputations needed to unlock *this* NPC. Each object might specify an `npcId` and `requiredRep`.
* **`preferredEffects`:** An array defining effects an NPC might ask for. Each entry includes:
    * `EffectsName`: The name of the effect.
    * A probability value (see [Effects (Necessary & Optional)](#effects-necessary--optional) section for how this determines if it's optional or necessary and its chance).
    * `unlockRep`: Reputation needed with this NPC to unlock this effect preference.
    * You can include an effect named `"Random"` (usually at the end of the list) to have the mod pick a random effect from the global effects list.
* **`products` Array:** Defines the drugs an NPC deals with. Each product entry includes:
    * `drugType`: Name of the drug (must match `s1api` strings).
    * `qualities` Array: Lists qualities for this drug.
        * **Must be sorted from worst to best.**
        * Each quality has an `unlockRep`.
        * `qualityName` (must match `s1api` strings).
        * `dollar_mult` (for reward calculation).
    * `EffectsName` within a product context is generally for reference or if a specific product variant always has certain intrinsic effects (though quest effects are driven by `preferredEffects`).
* **`shippingTiers` Array:** Defines different tiers of order sizes and deal modifiers. Each tier object contains:
    * `unlockRep`: Reputation needed to unlock this shipping tier.
    * `minAmount`, `maxAmount`: Base min/max order quantities for this tier.
    * `repLogBase`: The base for the logarithm used in scaling order amounts with reputation for this tier. A value of `0` disables log scaling for this tier, using `minAmount`/`maxAmount` directly (plus 1 for rep if not 0 based).
    * `dealsModifier`: An array of 4 numbers. These act as multipliers for the corresponding elements in the `deals` array chosen for a quest.
* **`deals` Array:** An array of possible deal structures. One is randomly chosen for a quest. Each deal structure is an array of 4 elements:
    1.  `TimeLimitInDays`: Duration to complete the quest.
    2.  `RewardMultiplier`: A general multiplier for the quest's rewards (`dealTimesMult` in reward formula).
    3.  `CashPenalty`: Flat cash penalty for failing the quest.
    4.  `RepPenalty`: Reputation penalty with the NPC for failing the quest.
    * **Interaction with `dealsModifier`:** For a chosen `deals` array and the active `shippingTier`'s `dealsModifier` array:
        `final_deal_parameter[i] = deals[i] * shippingTiers.dealsModifier[i]`
        This calculated product becomes the actual parameter used for the quest (e.g., modified time limit, modified reward multiplier, etc.).
* **`dialogues` Object:** Contains key-value pairs for different dialogue situations (e.g., `intro`, `dealStart`, `success`). See [Dialogues](#dialogues) for placeholders.
* **Reward Configuration:**
    * `base_dollar`, `base_rep`, `base_xp`: Base reward amounts for any successful deal with this NPC.
    * `price_mult`, `rep_mult`, `xp_mult`: Multipliers used in the detailed reward calculations.

// after calculating log with replogbase, 6 is minimum required to get to 1 multiplier, ie. 5 is subtracted but result cannot be -ve

> **Note:**  Currently all dollar_mult is taken from list at the top of the json and added with dollar_mult from quality/effects fields

> **Note:** If you wish to load dealers from all JSON files in the Empire folder and its subfolders, this may be later supported as an optional advanced feature. For advanced organization, optional idea - not yet implemented - restructure your empire JSONs into separate folders, with each folder containing NPC-specific JSON files and icons.

### Important JSON Rules & Assumptions

* **Initial Unlock:** Each NPC must have at least one drug type + quality unlocked at `unlockRep: 1` and at least one `shippingTier` unlocked at `unlockRep: 1`. This ensures the player can start building reputation with them.
* **String Matching:** `DrugType`, `EffectsName`, and `QualityName` strings in your JSON **must exactly match** the corresponding strings used by `s1api` and the base game. These are provided at the top of reference JSON files.
* **`deals` and `dealsModifier` Size:** Both the `deals[index]` array and the `dealsModifier` array within a `shippingTier` must have 4 elements.

> **Note:** The current code requires both a first and last name for each NPC. This limitation will be removed in a future update when supported by the underlying API.

## JSON Editor Tool

To aid in the creation and management of complex NPC configurations, this mod includes a **JSON Editor tool**. This tool is designed to help you:

* Merge multiple NPC JSON files.
* View and edit NPC parameters in a more user-friendly interface than a raw text editor.
* (Further details about the tool's capabilities and usage can be provided with the tool itself.)

## Current Development Status

### Pending Core Features

* **Deal XP Rewards:** Implementation of XP rewards for completing deals is pending support from the `s1api`.

### Current Gameplay Notes & Limitations

* **Single Active Quest:** Only one quest from this mod can be active at a time.
* **Quest Generation:** One quest is generated per NPC when quests are refreshed.
* **Random Product Choice:** If multiple definitions of product type exist in the JSON, one is chosen at random for a quest.
* **Max Quality Orders:** NPCs only order up to the maximum quality for each product type that the player has unlocked with them based on current reputation.
* **Effect Logic:** Effects are chosen randomly based on probabilities defined in the `preferredEffects` section of an NPC's JSON.

## Roadmap & Future Ideas

This mod is an ongoing project with many plans for expansion and refinement!

### Ongoing Development Tasks

* **Code Review:** Regularly re-visit `TODO` (potential changes/improvements/additions) and `UPDATABLE` (code sections needing updates with new game versions) comments in the source code.
* **Retroactive Compatibility:** For new JSON fields added in updates, ensure default values are created and fields are nullable to support older save files and NPC configurations.
* **Content Expansion:** Continuously work on:
    * Creating new default NPCs.
    * Balancing and Gamifying existing NPC economies and progression.
    * Adding game elements.
    * Introducing more product and effect variety through JSON configurations.

### Planned In-Game UI Enhancements (Part of this Mod)

* **NPC Information Panel:** An in-game UI panel to display:
    * NPC relations.
    * Unlock progress for products, qualities, shipping tiers, and effects.
* **Shipping Tier Upgrades:**
    * Display costs for unlocking new shipping tiers.
    * Allow players to press a button to deduct the fee and upgrade their shipping tier with an NPC.
    * A dedicated Shipping Upgrade Button UI is planned for easier upgrades.
* **UI Bug Fixes:** Address issues like the "cancel current delivery" button behavior after quest completion/refresh.
* **General UI Improvements:** Enhance the overall user interface for better clarity and ease of use.

### Future Concepts & Possibilities (Optional/Ideas/Pending Implementations)

* **New NPC Archetypes:**
    * NPCs like "Bicky Robby." - JSON field Support
    * Cartel-affiliated NPCs with unique questlines or mechanics (e.g., forced quests, debt payoff).
    * A static "Blackmarket Buyer" NPC who buys any products the player has discovered.
* **Quest Generation Enhancements:**
    * Variable quest generation frequency (e.g., not always one per NPC, but a maximum of one per product type).
    * Free daily quest refreshes.
    * Variable cooldowns or "days of order" for quest regeneration.
    * NPCs ordering from a list of "Product Manager Discovered Products" or "Favorited Products." - Json support
	* Scale refresh cost with unlocked NPCs
* **Deeper NPC Immersion:**
    * Unlock criteria based on player rank, wealth, or total deals completed.
    * Custom NPC avatars and appearances.
    * NPCs physically spawning and moving in the game world (**"Real NPCs"** - requires restructuring NPC code if `s1api` supports individual `onloaded` events for derived NPC classes).
* **Advanced Interactions:**
    * NPC relationship matrix (potentially affecting other mod or base game NPCs).
    * Dialogue-driven missions and storylines.
* **API Dependent Features:**
    * Replace hardcoded prices with base prices from product definitions once `s1api` supports this.
* **New Mechanics:**
    * A "Gift" button/system to improve relations with NPCs.
    * "Police Heat Level" associated with quests, potentially modifying rewards or risks.
* **Order Amount Scaling:**
    * Optionally, the logarithmic scaling for order amounts (`repLogBase`) could be offset, softcoded, or the equation changed for more flexibility.
* **Quest Rewards:**
    * Optionally, players may receive a bonus for turning in a quest earlier than the deadline.
* **Quality Handling:**
    * Until the game releases "heavenly meth," the "premium" quality will be treated as equivalent to "heavenly" for meth orders (this is handled in code as well).
	
// Optional QOL - possibly restructure json quality and effects from 2 list to 1 dictionary format
// Optional - Add probability field in quality and switch on that instead of random equal weightage - OR make a global field+local field hybrid
// Optional - X button on each quest to dismiss it without any penalty
// Optional - Create separate file on Code and JSON fields info in git accesible by link
// Optional - Add scrollable to right side detail panel 

## For Developers & Content Creators

* **Source Code Comments:** Pay attention to:
    * `UPDATABLE` comments: These mark parts of the code that may need adjustment when the base game (Schedule 1) or `s1api` receives significant updates.
    * `TODO` comments: These highlight areas where improvements, new features, or changes are planned or could be implemented.
* **Content Creation:** You are encouraged to create and share your own NPC JSON packs and git pull or send me in dicord suggested edits!
* **Testing Checklist (when creating/modifying content):**
    * NPC progression (unlocks, reputation gains).
    * Reward calculations (money, rep).
    * Logarithmic scaling of order amounts.
    * Effect logic (optional/necessary selection during delivery and impact on rewards).
    * Save and load functionality with custom NPCs.
    * Attempting to drop non-quest items(type, quality, effects, non product) in the delivery zone.
    * Quest cancellation logic and penalties.
    * Ensure that if a product type is meth and the required quality is "heavenly," the system treats "premium" as acceptable.
    * When calculating rewards, pay for the ordered quality and up to the maximum quality in the list.
    * If a necessary effect or quality is not satisfied, the quest should continue rather than fail immediately.
    * Update the effect sum with any optional effect multipliers present.
    * NPC responses should reflect the product type, quality, and necessary effects requested.
    * When updating to new versions, change placeholder/dummy product effects and qualities to real effects from `s1api` once supported, and implement rewards based on effects and quality.


## Checklist to Release

- [x] Fix quest removal logic bug (index)
- [x] Fix -inf amount bug due to log
- [x] Show reputation of dealers below rewards (temporary fix)
- [x] UI Bug Fixes: Address issues like the "cancel current delivery" button behavior after quest completion/refresh
- [x] Rep log offset: hardcode (5=0, 6=1)
- [x] Real NPC restructure
- [x] NPC balancing
- [x] Mix up base rewards and penalties and multipliers with roll from 50% to 150%
- [x] Optional: Give order of any quality
- [x] Round to 4 decimal places in UI
- [x] Reduce base reward and deal multiple
- [x] Only 1 quest per dealer
- [ ] Note: Until game releases heavenly meth, premium = heavenly for meth (in code too)
- [x] Basic Testing
- [ ] Play game
- [x] Version update to s1api 1.7 when stable released
- [x] Change placeholder dummy product effects and quality with real effects from s1api once supported; implement rewards based on effects and quality; check and reward product type
- [x] NPC responses on product [type], quality, necessary effects
- [x] Continue if necessary effect or quality not satisfied
- [x] Update EffectSum with optional effect multiplier present
- [x] If product type is meth and quality required is heavenly, give a pass
- [x] Pay for ordered quality and up to max quality in list
- [x] There are 3 random ranges in which rewards, rep and xp will be scaled.
- [x] Round up UI stuff to most significant digits 
- [x] Add UI under rep to show deal dates
- [x] Check enum with string before quest generation
- [x] Blackmarketbuyer UI method needs lowercase and trim
- [x] Fix the after quest complete, quest can be cancelled bug
- [x] Rep Unlock Design
- [x] Update JSONEditor with dealDates
- [x] Update Json and code with curfewDeal:true
- [ ] Update JSON Editor for curfewDeal
- [x] change random effects multiplier to static scaler 
- [x] add random base price multiplier to price multiplier
- [x] Give XP thru temp method


💕Credits:
Much gratitude and many many thanks to:
❤️ @Akermi Sensei for teaching S1API usage through his git repos and for the initial project structure.
❤️ S1API for providing the foundation for the mod, and it's creators @KaBooMa @Akermi @Max @ChloeNow  for making such a valuable and user-friendly modding resource.
❤️ @Freshairkaboom for helping with the UI.
❤️ @iiTzSamurai for App icon and delivery icons.
❤️ Tyler for giving us Schedule 1.
❤️ Breaking Bad for the NPC inspirations.
❤️ Me for sticking through and making my first full game mod.
❤️ AI
 
---

We hope you enjoy the NPC Custom Buyers & Dealers Expansion Mod! Your feedback and contributions are welcome.

// Add image resizing for all icon loading, 127 x 127
// Optional - Upgrades on unlocking NPCs
// Optional - NPC relations affect other NPC relations
// Convert Quest Data effects to dictionary
// add random and static values to load from json
// Update Readme