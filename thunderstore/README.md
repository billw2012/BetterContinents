# Better Continents: a Valheim Modification

## **[Download on Nexusmods](https://www.nexusmods.com/valheim/mods/446) | [Full Documentation](https://billw2012.github.io/BetterContinents-Docs/introduction.html) | [Installation Guide](https://billw2012.github.io/BetterContinents-Docs/setup-guide.html) | [Discord](https://discord.gg/3XW8ZntYzN)**

## **Introduction**
This mod provides the tools to both improve the standard terrain generation of the game, and override aspects of it with image maps, allowing precise control over height, biomes, location placement, and forest coverage.  
These generated worlds can be shared with others (who have this mod installed) by simply copying the usual world files, along with the extra `.BetterContinents` file.  

> See maps people have shared [here](https://www.nexusmods.com/valheim/mods/categories/13/).

> Check the links above for documentation and installation instructions.

---
## **Features**
* Use image files as [base heightmap](https://billw2012.github.io/BetterContinents-Docs/settings/heightmap.html#heightmap-file) layer, [detail heightmap](https://billw2012.github.io/BetterContinents-Docs/settings/flatmap.html#flatmap-file) layer, and [biome specific heightmap](https://billw2012.github.io/BetterContinents-Docs/settings/roughmap.html#roughmap-file) layer, with blending options for each
* A comprehensive layer based noise system based on [FastNoiseLite](https://github.com/Auburn/FastNoise), including support for any number of layers of noise with separate settings, each with its own distortion and mask
* A preset system with a few inbuilt defaults, allowing easy selection and sharing of world configs (anyone can make and share their own presets)
* An in game UI for designing your world, with visualisation for the layer system, and fast updating of the terrain and map
* An extensive console command system matching the UI, for those who prefer it
* Use an [image file to specify biomes](https://billw2012.github.io/BetterContinents-Docs/settings/biomemap.html#biomemap-file)
* Use an [image file to specify spawning positions](https://billw2012.github.io/BetterContinents-Docs/settings/spawnmap.html#spawnmap-file) for locations, including player start, bosses, and trader
* Use an [image file to specify forest coverage](https://billw2012.github.io/BetterContinents-Docs/settings/forest.html#forestmap-file), including in [biomes that normally are totally forested](https://billw2012.github.io/BetterContinents-Docs/settings/forest.html#forest-factor-overrides-all-trees) (Swamp, Mistlands, Dark Forest, Mountains)
* Change [global scale](https://billw2012.github.io/BetterContinents-Docs/settings/global.html#continent-size) – changes continent sizes
* Adjust [sea level](https://billw2012.github.io/BetterContinents-Docs/settings/global.html#sea-level-adjustment)
* Adjust [forest scaling](https://billw2012.github.io/BetterContinents-Docs/settings/forest.html#forest-scale) (how big the contiguous areas of forest/clearings are), and amount
* Specify [starting position](https://billw2012.github.io/BetterContinents-Docs/settings/start-position.html) explicitly
* Still allows loading vanilla maps, without needing to adjust any settings or disable the mod
* Backwards compatible (since version 0.2) – worlds created with older versions 
will still look the same when loaded with newer ones[*]Includes a debug mode to automatically enable cheats, reveal the full map, toggle 
on vanilla debugmode, and various other things, for quicker testing[*]Allows skipping of default location placement for quick world generation
* Export full map to png at any resolution
