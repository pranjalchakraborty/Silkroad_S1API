Project:
Let people easily create interlinked customisable npc's who act as buyers or dealers to expand the player's drug empire.
Each NPC has independent reputation, amount range of orders, dialogues, preferred necessary and optional effects.
Each NPC also has unlockable order amount and effects based on their rep. Other NPCs unlock based on prev NPC rep.
Delivery can be done in parts. Rewards can only be received at once - deliver all before time or fail.
Any JSON can be used for this mod. Icons can also be provided for NPC images.
A JSON Editor will be provided to easily merge and edit NPC JSONs.

Initial JSON NPCs are based on Breaking Bad.

Current Code:
Only one quest can be taken at a time. Complete it to take another.
Generates one quest for each npc for each drug type on quest refresh.
If multiple definitions of same product type exists, a random one will be chosen.
Only max quality for each product type - unlocked at current rep will be taken.
Effects will be randomly chosen with probability [(0+)-1] indicating chance out of 1 to be optional effect and [(1+)-2] -1 indicating chance out of 1 to be necessary effect. 

Reward Logic in existing Code:
money reward = 
base_dollar + (price of all products delivered)*(1+sum of all effects' dollar_mult)*(1+quality's dollar_mult)*(1+dealTimesMult)
rep reward = base_rep + money reward * rep_mult
xp reward = base_xp + money reward * xp_mult
xp reward is currently unsupported by s1api

UPDATABLE comments show parts of code to update with new game updates
TODO comments show parts of code that may be changed/improved/added to later

JSON Assumptions:
Each dealer has atleast one drug type+quality unlocked at rep 0 - to grow rep with him and one shipping tier at rep 0 - to decide initial quantity. 
Qualities need to be sorted in increasing order in the JSON.
Effects at rep 0 are optional.
Drug Type, Effects and Quality name strings should match S1API code strings.
dealTimes and dealTimesMult should be same size.penalties should be size 2.
Any Dialogue string can have (product}, {amount}, {quality}, {effects}, {optionalEffects} which will be replaced appropriately ingame and give info about the respective fields in the currently taken quest. amount is remaining amount.
dialogues - intro = first msg, dealStart= msg detailing delivery quest/product details, incomplete = every time you supply product but the total deal requirement amount is still not fulfilled/more is needed, fail - quest cancelled, expire - quest took too long to complete, success - msg when products in deal are received, reward - msg when rewards are sent after a delay
unlockRep is the reputation when the the drug or the quality or the necessary or optional effect is unlocked.
unlockRequirements are the set of previously unlocked npc reps and values that are required for the new npc to unlock.
dealTimes are the time in days which is given to the player for the deals - one amongst multiple options are rolled randomly .
dealTimesMult are the reward multiplier for each correponding dealTimes.
penalties are the dollar and rep penalties for failure.
reputation starts at 0 and can go upto any value.
bonus_dollar and bonus_rep are actually the base rewards for any deal irrespective of product ordered or amount.
while the mult fields are corresponding reward increases.

Before Update:
Complete Ongoing Quest.
Backup NPC data from save file.


Tasks:

Recurring:
re-visit TODO/UPDATABLE comments
create default values for new added json fields and make them nullable for retroactive save/load support
json - NPCs Creation, Balance, Gamification, More Products/Effects

MVP Functions:
// add deal xp when supported by s1api

Tests:
test - progression, rewards, effects/quality, save/load, put non items in drop

UI Tracker:
add ui panel to show relations, product, quality, shipping, effects unlocks with drop down selector for each npc
add shipping unlock costs - now unavailable - on button press deduct corresponding fee and call shipping upgrade function
// its saying cancel current delivery after ive completed the delivery and after refreshing the orders - remove listener, change button label
better UI

Optional/Ideas/Feedback/Usage:
// npcs - onboard bicky robby - cartel npc - force quests - blackmarket buyer as static npc with discovered products
// generate quests not always - at least 1 per npc - but change to max 1 per product from always 1 - free daily refresh - generate quest per variable day - days of order
// add json fields - order cooldown/generation day, order from product manager discovered products/ favorited products, unlock at rank/wealth/deals complete 
// add NPC avatars - appearances - spawn - dialogues 
// npc relations and dialogue missions - relation matrix - mod or base game npcs(?)



Checklist to Release:
= Effects as 0-2 with values determining constant vs roll of both necessary & optional effects 
=remove rep_mult and make xp and rep as mult of price - 
=type/name inconsistencies
= codebase consistency
= 1.5 update

// restructure json - deal related and penalty json - field in shipping(modifier) and in deals
// asymptotic scaling with rep - shipping and price - log base x as multiplier(?)
// random product from product manager - random necessary/optional effects on top

// json - NPCs Creation, Balance, Gamification, More Products/Effects
// restructure json for new probability

// update json Tools
// Minimal UI

// Remove Silkroad references - make it Empire
// create real NPCs - restructure to derived of base npc if s1api onloaded works for each

// version update to s1api 1.7 when stable released
// change placeholder dummy product effects and quality with real effects from s1api once supported - Implement rewards based on effects and quality

