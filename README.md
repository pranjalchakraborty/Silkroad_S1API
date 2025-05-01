Project:
Let people easily create interlinked customisable npc's who act as dealers to expand the player's drug empire.
Each NPC has independent reputation, amounts range of orders, dialogues, preferred necessary and optional effects.
Each NPC also has unlockable order amount and effects based on their rep. Other NPCs unlock based on prev NPC rep.
Each NPC has a resetSave that should be false. If set to true, it resets all save data related to that NPC in the mod. Useful if JSON changed midsave.
Delivery can be done in parts. Rewards can only be received at once - deliver all before time or fail.

Any JSON can be used for this mod. Icons can also be provided for NPC images.
A JSON Editor will be provided to easily merge and edit NPC JSONs.

Initial JSON is based on Breaking Bad.

Current Code:
Only one quest can be taken at a time. Complete it to take another.
Generates one quest for each npc for each drug type.
If multiple definitions of same product type exists, a random one will be chosen.
money reward = bonus_dollar + (price of all products delivered)*(base_dollar_mult+sum of all effects' dollar_mult)*(1+quality's dollar_mult)
rep reward = bonus_rep + (price of all products delivered)*(base_rep_mult+sum of all effects' rep_mult)*(1+quality's rep_mult)
Multiply both by (1+dealTimesMult)
UPDATABLE comments show parts of code to update with new game updates

JSON Assumptions:
Each dealer has atleast one drug type+quality unlocked at rep 0 - to grow in rep with him and one shipping tier at rep 0 - to decide initial quantity. 
Qualities are sorted in increasing order in the JSON.
Effects at rep 0 are optional.
Drug Type, Effects and Quality name strings should match S1API code strings.
Any Dialogue string can have (product}, {amount}, {quality}, {effects}, {optionalEffects} which will be replaced appropriately ingame.
dealTimes and dealTimesMult should be same size.penalties should be size 2.




Tasks:

Functions:
\\change placeholder dummy product effects and quality with real effects from s1api once supported
Implement rewards based on effects and quality 
re-visit TODO comments

Design:
NPCs,Dialogues,Balance
json - balancing and design, dialogues -reward,success,incomplete, dealStart

=deal time limits, incomplete, success - dialogues - code
=quest app dialogues - code 
=update for s1api - silkroad
=move drug type to from unique types 
=icon
=ui changes
=make refresh cost 420 and refrer implementation
=quest expiry func sub/unsub
=remove taken quest
=remove free refresh on cancel


Optional:
//make expiry penalty a percentage of the deal, or tiered by rep/rank etc.
//free daily refresh and on load
//onboard bicky robby
//cartel npc - force quests
//partial json fields - days of order, order cooldown, order from product manager discovered products/ favorited products, unlock at rank
//create real NPCs
//add NPC avatars


Tests:
test - progression, rewards, save/load



UI Tracker:
add ui panel to show relations, product, quality, shipping, effects unlocks with drop down selector for each npc
add shipping unlock costs - now unavailable - on button press deduct corresponding fee and call shipping upgrade function
replace refresh with management button



UI/Performance Improvements:
//add button in app to check if NPC unlock criteria met/update - Initialize()/Update from JSON in delivery rewards to be replaced
//better UI
//Dealer Image on Quest/Journal/DeadDrop Icons

