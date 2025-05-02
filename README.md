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
Effects will be random.
Reward Logic in existing Code:
money reward = bonus_dollar + (price of all products delivered)*(base_dollar_mult+sum of all effects' dollar_mult)*(1+quality's dollar_mult)
rep reward = bonus_rep + (price of all products delivered)*(base_rep_mult+sum of all effects' rep_mult)*(1+quality's rep_mult)
Multiply both by (1+dealTimesMult)
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
probability=1 in effects means those are necessary effects in the product to be accepted. optional means you may give them to get the added rewards as expressed in mult field.

Tools:
JSON editor/manipulator, splitter based on dealer and combiner into empire.json

Tasks:

Recurring:
re-visit TODO/UPDATABLE comments

MVP Functions:
\\ change placeholder dummy product effects and quality with real effects from s1api once supported
\\ Implement rewards based on effects and quality 

Design:
NPCs Balance, Gamification - json



Optional/Ideas/Feedback:
// make expiry penalty a percentage of the deal, or tiered by rep/rank etc.
// free daily refresh
// onboard bicky robby
// cartel npc - force quests
// partial json fields - order cooldown, order from product manager discovered products/ favorited products, unlock at rank
// create real NPCs - restructure to derived of base npc if s1api onloaded works for each  - add NPC avatars
// Dealer Image on Quest/Journal/DeadDrop Icons - do we  want it?
// generate quests not always - at least 1 per npc - but change to max 1 per product from always 1
// generate quest per variable day - days of order


Tests:
test - progression, rewards, effects/quality, save/load, put non items in drop



UI Tracker:
add ui panel to show relations, product, quality, shipping, effects unlocks with drop down selector for each npc
add shipping unlock costs - now unavailable - on button press deduct corresponding fee and call shipping upgrade function
also, its saying cancel current delivery after ive completed the delivery and after refreshing the orders
// better UI




JSON splitter, save/load, combiner, editor workflow Choices:
1. Split JSON then edit in web link = Possible
2. Compile Git repo and splitter = Unknown 
3. Create own splitter and editor = Done