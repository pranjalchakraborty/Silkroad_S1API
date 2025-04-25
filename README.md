Let people easily create interlinked customisable npc's who act as dealers to expand the player's drug empire.
Initial JSON Breaking Bad based.

Tasks:
//create json_editor.exe for creating and saving and interlinking npcs

change amount checking from bricks to real amount
add logic for accept dropped product based on necessary (unlocked/ordered) effects
add logic for rep and money rewards based on ordered effects ( rep = amount*(1+sum of rep_mult)  money = amount*price*(1+sum of price_mult) )



test - progression, rewards
add buttons to force complete taken quest - testing

add ui panel to show relations, product, quality, shipping, effects unlocks with drop down selector for each npc
add button in app to check if unlock criteria met/update - Initialize()/Update from JSON
add shipping unlock costs - now free - on button press deduct corresponding fee
add drop quest button 
configure costs (money,rep) for dropping and refreshing quests

//number(min,max) of quests to generate per dealer - numbers
product id type quests to generate using product manager - bool
effects type quests to generate - bool
number of quests that can be taken parallely


