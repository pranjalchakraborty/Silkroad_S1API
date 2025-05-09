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

* **Reputation System:** NPC reputation starts at `1` and can increase indefinitely. Higher reputation unlocks more benefits.
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
    `money = base_dollar + (total_price_of_delivered_products * (1 + sum_of_all_effects_dollar_mult) * (1 + quality_dollar_mult) * dealTimesMult)`
* **Reputation Reward:**
    `rep = base_rep + (money_reward * rep_mult)`
* **XP Reward:**
    `xp = base_xp + (money_reward * xp_mult)`
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

1.  **Complete Ongoing Quests:** Finish or cancel any active delivery quests from this mod.
2.  **Backup NPC Data:** It's highly recommended to backup this mod's NPC-specific data from your save file. (Specify path or method if known, otherwise general advice).

## Creating & Customizing NPCs (JSON Guide)

### Overview

This mod uses JSON (JavaScript Object Notation) files to define all aspects of NPCs, their quests, and progression. You can create your own NPCs or modify existing ones by editing these JSON files.

* **Manual Editing:** Use any text editor (like Notepad++, VS Code, etc.).
* **JSON Editor Tool:** A dedicated JSON Editor tool is provided with this mod to help you easily merge, view, and edit NPC JSON configurations.

### Key JSON Concepts & Fields

Below are some of the crucial fields and structures you'll encounter in the NPC JSON files:

* **NPC Object:** Each NPC is typically an object within a larger JSON array.
* **`reputation` (Initial):** While reputation is tracked dynamically in-game, NPCs effectively start at `1`.
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

### Important JSON Rules & Assumptions

* **Initial Unlock:** Each NPC must have at least one drug type + quality unlocked at `unlockRep: 1` and at least one `shippingTier` unlocked at `unlockRep: 1`. This ensures the player can start building reputation with them.
* **Reputation 1 Effects:** Any effects available at `unlockRep: 1` should generally be configured as optional (probability `[0,1)`).
* **String Matching:** `DrugType`, `EffectsName`, and `QualityName` strings in your JSON **must exactly match** the corresponding strings used by `s1api` and the base game. These are often provided at the top of reference JSON files.
* **NPC Definition Order (CRITICAL):** In your JSON file(s) containing multiple NPCs, **NPCs MUST be sorted according to their `unlockRequirements`**. An NPC should appear *after* any NPCs that are prerequisites for its unlock. Failure to do so will result in NPCs not unlocking correctly at game start; they will only unlock dynamically if their prerequisites are met through gameplay *after* the game has loaded.
* **`deals` and `dealsModifier` Size:** Both the `deals[index]` array and the `dealsModifier` array within a `shippingTier` must have 4 elements.

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
* **Quest Generation:** One quest is generated per NPC for each drug type they handle when quests are refreshed.
* **Random Product Choice:** If multiple definitions of the same product type exist in the JSON, one is chosen at random for a quest.
* **Max Quality Orders:** NPCs only order up to the maximum quality for each product type that the player has unlocked with them based on current reputation.
* **Effect Logic:** Effects are chosen randomly based on probabilities defined in the `preferredEffects` section of an NPC's JSON.

## Roadmap & Future Ideas

This mod is an ongoing project with many plans for expansion and refinement!

### Ongoing Development Tasks

* **Code Review:** Regularly re-visit `TODO` (potential changes/improvements/additions) and `UPDATABLE` (code sections needing updates with new game versions) comments in the source code.
* **Retroactive Compatibility:** For new JSON fields added in updates, ensure default values are created and fields are nullable to support older save files and NPC configurations.
* **Content Expansion:** Continuously work on:
    * Creating new default NPCs.
    * Balancing existing NPC economies and progression.
    * Adding gamification elements.
    * Introducing more product and effect variety through JSON configurations.

### Planned In-Game UI Enhancements (Part of this Mod)

* **NPC Information Panel:** An in-game UI panel to display:
    * NPC relations.
    * Unlock progress for products, qualities, shipping tiers, and effects (possibly with a dropdown selector per NPC).
* **Shipping Tier Upgrades:**
    * Display costs for unlocking new shipping tiers.
    * Allow players to press a button to deduct the fee and upgrade their shipping tier with an NPC.
* **UI Bug Fixes:** Address issues like the "cancel current delivery" button behavior after quest completion/refresh.
* **General UI Improvements:** Enhance the overall user interface for better clarity and ease of use.

### Future Concepts & Possibilities (Optional/Ideas)

* **New NPC Archetypes:**
    * Special NPCs like "Bicky Robby."
    * Cartel-affiliated NPCs with unique questlines or mechanics (e.g., forced quests).
    * A static "Blackmarket Buyer" NPC who buys any products the player has discovered.
* **Quest Generation Enhancements:**
    * Variable quest generation frequency (e.g., not always one per NPC, but a maximum of one per product).
    * Free daily quest refreshes.
    * Variable cooldowns or "days of order" for quest regeneration.
    * NPCs ordering from a list of "Product Manager Discovered Products" or "Favorited Products."
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

## For Developers & Content Creators

* **Source Code Comments:** Pay attention to:
    * `UPDATABLE` comments: These mark parts of the code that may need adjustment when the base game (Schedule 1) or `s1api` receives significant updates.
    * `TODO` comments: These highlight areas where improvements, new features, or changes are planned or could be implemented.
* **Content Creation:** You are encouraged to create and share your own NPC JSON packs!
* **Testing Checklist (when creating/modifying content):**
    * NPC progression (unlocks, reputation gains).
    * Reward calculations (money, rep).
    * Logarithmic scaling of order amounts.
    * Effect logic (optional/necessary selection during delivery and impact on rewards).
    * Save and load functionality with custom NPCs.
    * Attempting to drop non-quest items in the delivery zone.
    * Quest cancellation logic and penalties.

---

We hope you enjoy the NPC Custom Buyers & Dealers Expansion Mod! Your feedback and contributions are welcome.



Checklist to Release:
= Effects as 0-2 with values determining constant vs roll of both necessary & optional effects 
=remove rep_mult and make xp and rep as mult of price - 
=type/name inconsistencies
= codebase consistency
= 1.5 update
= restructure json for new probability
= Make Dialogue instead of Task visible in App
= restructure json - deal related and penalty json - field in shipping(modifier) and in deals
= asymptotic scaling with rep - shipping - log base x as multiplier(?) - 0 is off
= random necessary/optional effects on top - Random = rolls extra unset effect with same rules as specified effects
= Code Changes to support above 3 json changes
= improve initialize npc json logic on reward received
= update json Tools
= json - NPCs Balance, Gamification, More Products/Effects, Proper Effect Names
= add drugs, quality in JSON
= Remove Silkroad references - make it Empire
// Basic Testing

// Minimal UI - Shipping Buttons/Upgrade

// version update to s1api 1.7 when stable released
// change placeholder dummy product effects and quality with real effects from s1api once supported - Implement rewards based on effects and quality - check and rewards

