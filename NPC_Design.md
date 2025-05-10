## AI NPC Design Instructions & Recommendations

This document outlines the principles, patterns, and balancing considerations for designing new Non-Player Characters (NPCs) for the "NPC Custom Buyers & Dealers Expansion Mod" for Schedule 1. The goal is to ensure new NPCs are thematically consistent, balanced for player progression, and offer engaging gameplay.

### I. General Principles & Philosophy

1.  **Player Progression is Key:** NPCs should unlock and scale in a way that provides a smooth and rewarding progression curve for the player. Early NPCs should be accessible,
    while later-tier NPCs should present greater challenges and offer more significant rewards.
2.  **Thematic Cohesion (Breaking Bad Inspired):** While not strictly mandatory for every NPC, drawing inspiration from the source material (Breaking Bad) for character roles, drug preferences, dialogue snippets (though dialogues are usually pre-defined), and shipping tier names can enhance immersion. Balance this with gameplay needs.
3.  **Gamification & Balance Over Strict Realism:** Prioritize engaging gameplay loops and balanced rewards over strict adherence to realism. The economy should feel rewarding but not easily exploitable.
4.  **Interspersed Unlocks:** Avoid unlocking all content for an NPC (all drugs, all qualities, all effects, all shipping tiers) at a single reputation level. Stagger these unlocks to provide continuous small goals and rewards as the player builds reputation.
5.  **Drug Specialization & Variety:**
    * NPCs can specialize in one or more of the three core drugs: Weed, Meth, Cocaine.
    * Ensure a good distribution of drug types across the available NPCs.
    * Later-tier NPCs should generally deal in higher quality drugs and/or larger volumes.
6.  **Clear Tiering:** NPCs should implicitly or explicitly fall into tiers (early, mid, late, end-game). This tiering should be reflected in their unlock requirements, the difficulty of their demands, and the value of their rewards.

### II. `unlockRequirements`

1.  **Prerequisites:** NPCs (except for the very first starting NPCs) should require a minimum reputation (`minRep`) with one or more existing NPCs (`name`) to unlock.
2.  **Logical Progression:** Ensure the prerequisite NPCs make sense thematically and in terms of game progression. A player should naturally encounter and build reputation with earlier NPCs before unlocking later ones.
3.  **`minRep` Values:**
    * Early game unlocks might require `minRep` values between 5-15.
    * Mid-game unlocks might require `minRep` values between 10-30.
    * Late-game unlocks can require significantly higher `minRep` values (e.g., 25-50+).
    * Consider the average number of deals a player might need to complete with a prerequisite NPC to reach the `minRep` for the next.

### III. `deals` Array (Time, Reward Multiplier, Penalties)

1.  **Structure:** Each deal is an array: `[TimeLimitInDays, RewardMultiplier, CashPenalty, RepPenalty]`.
2.  **Variety:** Offer 2-3 deal options per NPC to provide some randomness.
3.  **Time Limit vs. Reward/Penalty:**
    * Shorter time limits (e.g., 1-2 days) can have slightly higher `RewardMultiplier` (e.g., 1.1 - 1.5) and lower penalties.
    * Longer time limits (e.g., 3-7 days) should have lower `RewardMultiplier` (e.g., 0.8 - 1.0) and higher penalties for failure. This balances risk/reward.
4.  **Scaling Penalties:** Cash and Rep penalties should scale with the NPC's tier and the potential value of the deal.
    * Early NPCs: Lower penalties (e.g., Cash: 250-500, Rep: 5-10).
    * Mid-NPCs: Moderate penalties (e.g., Cash: 750-2000, Rep: 8-20).
    * Late-NPCs: High penalties (e.g., Cash: 2500-10000+, Rep: 15-50+).
5.  **Special Cases (e.g., Saul Goodman):** Unique mechanics like negative `RepPenalty` (where failing *improves* rep, or rather, doesn't hurt it as much as expected) can be used sparingly for specific character concepts, but ensure they are balanced.

### IV. `repLogBase` (Reputation Logarithmic Scaling for Order Amounts)

1.  **Purpose:** Controls how quickly an NPC's order `minAmount` and `maxAmount` (from `shipping` tiers) scale with player reputation. Formula: `(log_base(Reputation) + 1) * base_amount`.
2.  **Values:**
    * `0`: No logarithmic scaling. Order amounts are solely determined by the `minAmount`/`maxAmount` in the current shipping tier. Typically used for very late-game NPCs whose volume is defined by large, distinct shipping tiers rather than gradual rep scaling.
    * `30-60`: Good for early to mid-game NPCs. A lower `repLogBase` (e.g., 30) means faster scaling with reputation. A higher value (e.g., 60) means slower scaling.
    * `60-100+`: Can be used for mid-to-late game NPCs where you want reputation to still matter for volume, but less aggressively than early NPCs.
3.  **Balancing Consideration:** If an NPC has `repLogBase: 0`, their progression in terms of order volume is entirely tied to unlocking new, more substantial `shipping` tiers.

### V. `drugs` Array (Per Drug Configuration)

1.  **`type`:** "weed", "meth", or "cocaine".
2.  **`unlockRep` (for the drug itself with this NPC):**
    * Primary drug(s) for an NPC usually unlock at `0` rep (or the NPC's own unlock requirement).
    * Secondary or tertiary drugs can unlock at higher reputation levels with that NPC (e.g., 10, 15, 20, 40+ rep).
3.  **`base_dollar`:**
    * Should roughly align with the average in-game sale price of that drug type *at a baseline quality*.
    * Weed: \~100-150
    * Meth: \~200-250
    * Cocaine: \~400-450
    * Can be slightly higher for later-tier NPCs or for drugs that unlock later with a specific NPC to incentivize the player.
4.  **`base_rep`:**
    * The flat reputation awarded for a successful deal, *before* the `rep_mult` is applied to the money reward.
    * Early NPCs (primary drug): \~4-6.
    * Mid-NPCs (primary drug): \~7-12.
    * Late-NPCs (primary drug): \~15-30+.
    * Secondary/tertiary drugs unlocked later by an NPC can have a slightly higher `base_rep` than their primary drug to make them attractive.
5.  **`rep_mult` (CRITICAL FOR BALANCE):**
    * **Weed: `0.004`**
    * **Meth: `0.002`**
    * **Cocaine: `0.001`**
    * These values are set to normalize reputation gain across different drug price points (since cocaine is worth more, its multiplier is lower to yield comparable rep-per-dollar-value).
    * For *very* late-game NPCs where monetary rewards are exceptionally high, a blanket higher `rep_mult` (e.g., `0.01` as used for some later NPCs in the provided examples) can be considered if the goal is faster reputation gain with those specific high-tier characters, overriding the drug-specific multipliers. This should be used cautiously.
6.  **`xp_mult`:** Can be kept consistent (e.g., `0.01`) as XP is currently unsupported.

### VI. `qualities` Array (within each drug object)

1.  **Order:** Must be sorted from worst to best quality type (e.g., "trash", "poor", "standard", "premium", "heavenly").
2.  **`type`:** "trash", "poor", "standard", "premium", "heavenly".
3.  **`dollar_mult`:** A multiplier applied to the product's price based on its quality.
    * Trash: \~0.02
    * Poor: \~0.05
    * Standard: \~0.15 - 0.20
    * Premium: \~0.30 - 0.50
    * Heavenly: \~0.60 - 0.90+ (Reserved for top-tier product from elite NPCs).
4.  **`unlockRep`:**
    * The lowest quality (e.g., "trash" or "poor") should unlock at `0` rep (or when the drug itself unlocks for that NPC).
    * Higher qualities unlock at increasing reputation milestones with that NPC. Interleave these with effect unlocks and new drug unlocks.

### VII. `effects` Array (within each drug object)

1.  **`name`:** Must match an entry in the global `effectsName` list. "Random" is a valid entry.
2.  **`unlockRep`:** Reputation with the NPC required to unlock this specific effect request.
3.  **`probability`:**
    * `[0.0 - 1.0)` (e.g., 0.0 to 0.999...): Optional effect. The value is the chance (e.g., 0.7 = 70%).
    * `(1.0 - 2.0]` (e.g., 1.000...1 to 2.0): Necessary effect. Probability = (value - 1.0). So, `2.0` means 100% chance of being necessary. `1.5` means 50% chance of being necessary.
4.  **`dollar_mult` (for this specific effect):**
    * **Inverse Reward Principle:** Effects with low or zero in-game reward multipliers should have a higher `dollar_mult` in the mod (e.g., 0.50-0.80) to incentivize players to produce them.
    * Effects with high in-game reward multipliers should have a lower `dollar_mult` (e.g., 0.10-0.30).
    * Refer to the provided list of in-game effect multipliers to guide this.
5.  **"Random" Effects:**
    * Include 1-3 "Random" effect entries per drug, especially for mid to late-game NPCs.
    * These should always be optional (`probability` < 1.0).
    * Unlock them progressively.
    * The `dollar_mult` for "Random" effects can start low (e.g., 0.15-0.20) and increase for later "Random" unlocks (e.g., 0.25-0.45) to make them more appealing as the player progresses with the NPC.
6.  **Necessary vs. Optional Balance:** Ensure a mix. Early effects might be mostly optional. Later NPCs can demand more necessary effects.

### VIII. `shipping` Tiers

1.  **Number of Tiers:**
    * Early/Mid NPCs: Typically 2 tiers.
    * Late/End-Game NPCs (e.g., Jesse, Tuco, Gustavo, Heisenberg): Can have 3 tiers for more significant late-game scaling.
2.  **`name`:** Thematic names fitting the NPC.
3.  **`cost`:** Cost to unlock this shipping tier (player pays this once). Should scale significantly for higher tiers.
    * Tier 1: `0` (usually).
    * Tier 2: 5,000 - 25,000.
    * Tier 3: 50,000 - 150,000+.
    * Top End-Game Tier 3: Can be very high (e.g., 300,000 - 500,000+).
4.  **`unlockRep`:** Reputation with the NPC to unlock this tier.
5.  **`minAmount`, `maxAmount`:** Base order quantities for this tier. These are the values scaled by `repLogBase` (if > 0).
    * Ensure these scale logically between tiers. Tier 2 should offer significantly more than Tier 1, etc.
    * Consider in-game packaging sizes (1, 5, 20) when setting these and `stepAmount`.
6.  **`stepAmount`:** The increment for order amounts (often 1, 5, or 20, aligning with packaging).
7.  **`dealModifier`:** An array of 4 multipliers `[time_mod, reward_mod, cash_penalty_mod, rep_penalty_mod]`.
    * **Default/Standard:** `[1.0, 1.0, 1.0, 1.0]`. This means the shipping tier itself doesn't alter the base deal parameters; the NPC's main `deals` array dictates these.
    * Use values other than `1.0` sparingly and with clear intent (e.g., a very fast but risky shipping tier might have `time_mod: 0.5` but `reward_mod: 0.8` and higher penalty multipliers).
    * **Avoid `0.0` unless the intent is to completely nullify a parameter from the base `deals` array, which is generally not desired.**

### IX. Napkin Math & Iteration

* Before finalizing an NPC, do some quick calculations:
    * Average money per deal (consider `base_dollar`, typical product value, quality `dollar_mult`, effect `dollar_mult`).
    * Average rep per deal (using the above and `base_rep`, `rep_mult`).
    * Estimate how many deals it would take to unlock the next quality, effect, drug, or shipping tier for that NPC.
    * Estimate how many deals with prerequisite NPCs it would take to unlock *this* NPC.
* These estimations will help ensure the progression feels right and isn't too grindy or too fast. Adjust `unlockRep` values as needed.

By following these guidelines, new NPCs should integrate well into the existing framework, providing a balanced and engaging experience for players.
