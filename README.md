# BISBuddy

Gearing assistance plugin for FFXIV. Imports from gearset websites, highlights
required items in windows, and tracks inventory state.

## Features
* Importing gearsets
  * Etro
  * Xivgear
  * JSON
  * Automatic prerequesites builder using game data
* Highlight items
  * Need/greed loot windows

  ![Items highlighted in need/greed loot window](/images/needgreed.png?raw=true "Need/Greed Loot")

  * NPC shops/exchanges
  
  ![Items highlighted in NPC shop](/images/npcshopexchange.png?raw=true "NPC Shop/Exchange")
  
  * Materia melding
    
  ![Items and materia highlighted when melding](/images/materiamelding.png?raw=true "Materia Melding")

  * Marketboard

  ![Item listings highlighted in marketboard](/images/marketboard.png?raw=true "Marketboard Listing")
  
  * Player inventories
  
  ![Items highlighted in inventory window](/images/inventory.png?raw=true "Player Inventory")
  
  * Item tooltips
  
  ![Gearsets needing items listed on item tooltip](/images/itemtooltip.png?raw=true "Item Tooltip")
  
* Inventory state tracking
  * Assigns items to gearsets using linear assignment solver
  * Updates automatically on inventory state changes

## Installing

1. Open `/xlplugins`
2. Find "BISBuddy" under "All Plugins"
3. Click "Install"
    * Optionally configure with `/bisbuddy config`

## How To Use

1. Open plugin with `/bisbuddy`
2. Click "Add Gearset" and import desired gearset
3. Items needed to complete the gearset will be highlighted as they appear
    * When melding, select desired materia set to highlight melds
