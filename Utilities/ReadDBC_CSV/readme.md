# ReadDBC_CSV

**som_game_build** = 1.14.3.42770
**tbc_game_build** = 2.5.4.44833
**wrath_game_build** = 3.4.0.44996

# ReadDBC_CSV_Consumables - What it does
* It generates the available Food and Water consumables list based on the given DBC files.

## ReadDBC_CSV_Consumables - Required DBC files
* data/spell.csv
https://wow.tools/dbc/?dbc=spell&build=2.5.4.44833

* data/itemeffect.csv
https://wow.tools/dbc/?dbc=itemeffect&build=2.5.4.44833

## ReadDBC_CSV_Consumables - Produces
* data/foods.json
* data/waters.json


---
# ReadDBC_CSV_Spell - What it does
* It generates the available spell(id, name, level) list based on the given DBC file.

## ReadDBC_CSV_Spell - Required DBC files
* data/spellname.csv
https://wow.tools/dbc/?dbc=spellname&build=2.5.4.44833

* data/spelllevels.csv
https://wow.tools/dbc/?dbc=spelllevels&build=2.5.4.44833

## ReadDBC_CSV_Spell - Produces
* data/spells.json


---
# ReadDBC_CSV_Talents - What it does
* It generates the available talents based on the given DBC file.

## ReadDBC_CSV_Talents - Required DBC files
* data/talenttab.csv
https://wow.tools/dbc/?dbc=talenttab&build=2.5.4.44833

* data/talent.csv
https://wow.tools/dbc/?dbc=talent&build=2.5.4.44833

## ReadDBC_CSV_Talents - Produces
* data/talent.json
* data/talenttab.json


---
# ReadDBC_CSV_WorldMapArea - What it does
* It generates the WorldMapArea.json list based on the given DBC files.

## ReadDBC_CSV_WorldMapArea - Required DBC files
* data/uimap.csv
https://wow.tools/dbc/?dbc=uimap&build=2.5.4.44833

* data/worldmaparea.csv
https://wow.tools/dbc/?dbc=worldmaparea&build=2.4.3.8606

## ReadDBC_CSV_WorldMapArea - Produces
* data/WorldMapArea.json