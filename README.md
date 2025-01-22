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
  * Need/greed windows
  * NPC shops/exchanges
  
  ![Alt text](/images/npcshopexchange.png?raw=true "NPC Shop/Exchange")
  
  * Materia melding
    
  ![Alt text](/images/materiamelding.png?raw=true "Materia Melding")
  
  * Player inventories
  
  ![Alt text](/images/inventory.png?raw=true "Player Inventory")
  
  * Item tooltips
  
  ![Alt text](/images/itemtooltip.png?raw=true "Item Tooltip")
  
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
