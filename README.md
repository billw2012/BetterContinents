# Better Continents: a Valheim Modification

### [Better Continents on Nexusmods](https://www.nexusmods.com/valheim/mods/446)

## What?
This modifies the heightmap generation directly to attempt to give more interesting land masses.

## IMPORTANT: Backup your characters and worlds before using this (as you always should), and before you update.

---
## Features
- Change global scale -- changes continent sizes
- Adjust mountains amount
- Adjust sea level
- Additional customizable ridged heightmap layer gives more varied geography
- Use image files as base heightmap layer, with blending options
- Use image file to specify biomes 
- Still allows worlds that were created before this mod was installed to be loaded
- Adjust forest scaling (how big the contiguous areas of forest/clearings are), and amount
- Specify starting position explicitly

---
## Usage
1. Install the mod and run the game.
2. If you have configuration editor installed (if you use Vortex it installed automatically I believe):
   1. Press F1 to open it
   2. Find the Better Continents section and expand it
   3. Edit the parameters here:
        1. You can paste in the full path to the heightmap you want to use, there is no file browser
3. If you don't have the configuration editor then you should quit the game again and find the newly generated config file in the BepinEx/config directory.

---
## Notes
### Heightmap Usage

- The height map image must be square, and one of these sizes: 32. 64, 128, 256, 512, 1024, 2048, 4096.
- Most formats are supported, including HDR ones (anything Unity Texture Import supports).
- I recommend smoothing the image before using it to avoid any sharp changes in altitude. 
- Getting sealevel looking correct might take some tweaking, of either the image itself or the sealevel setting.

### Biomemap Usage

The size and format constraints are the same as the heightmap, however it doesn't need to be the exact same values/format as the heightmap you use. You can use a 4096x4096 HDR heightmap with a 64x64 png biomemap for instance.

The color codes for the biomes are as follows:

Biome | Hex code
--- | ---
<span style="color:#0000FF">Ocean</span> | #0000FF
<span style="color:#00FF00">Meadows</span> | #00FF00
<span style="color:#007F00">Black Forest</span> | #007F00
<span style="color:#7F7F00">Swamp</span> | #7F7F00
<span style="color:#FFFFFF">Mountains</span> | #FFFFFF
<span style="color:#FFFF00">Plains</span> | #FFFF00
<span style="color:#7F7F7F">Mistlands</span> | #7F7F7F
<span style="color:#00FFFF">Deep North</span> | #00FFFF
<span style="color:#FF0000">Ash Lands</span> | #FF0000

![image](https://raw.githubusercontent.com/billw2012/BetterContinents/main/misc/biome-colors.png)

When loading the biomemap, whichever color as specified above most closely matches the image color will determine which biome is used. Biomes cannot overlap, so the entire map is just one image.

#### Some biomemap authoring hints:
- Setup the above colors as swatches, or a stored palette, for easy access.
- Place your heightmap on a background layer, then set your biome layers to 50% transparent. 
- Use a separate layer for each biome, then you can reorder the layers if needed, and even apply layer edge effects.

Please share any of your own hints.

### When Settings are Applied
The settings you have specified (including the heightmap) are baked into the world when you first create it (when you press the "Create" button). Any further changes you make to the heightmap image or parameters will NOT affect existing worlds.

### Multiplayer Sync
The settings baked into the world (including the heightmap) are automatically transferred from server to client when clients connect, so you don't have to worry about them having differing settings (of course they DO need the mod installed though).

### Results May Vary
It is totally possible to make quite ugly terrain (massive sharp spikes etc.) when using the heightmap parameters, you should certainly use the debugmode flying to check out the quality of the terrain before commiting to playing a world if you are worried about this.

### Placement
You might want to check the log after creating your world to confirm it placed sufficient instances of buildings / spawns etc.

It isn't necessary for all of them to be placed (your game won't be affected noticably if it only placed 200 TrollCave02 out of 300), but you might want to confirm at least some of the ones you care about are.

The log file is at `C:\Users\<User Name>\AppData\LocalLow\IronGate\Valheim\Player.log`.

The log entries showing failure to place things look like this:
> 03/12/2021 12:57:39: Failed to place all GoblinCamp2, placed 188 out of 200

They are clustered together between the line:
> Checking for location duplicates

and
> Done generating locations, duration:50580.087 ms

The vanilla map generator can also fail to place all objects, it is not something specific to this mod, but of course with changes in the generation comes the chance for changes in placement success rate.

### Difficulty
I'm not sure if this makes the game more difficult or not. Certainly oceans can be a lot wider in some places, and just your starting continent might stretch over most of the map. But that means it likely contains all biomes and bosses.

### Reveal the whole map
Once you generate a new map you can view the whole result like so (you might want to confirm it meets your requirements):

1. Open the console with F5
2. Enter `imacheater` then `exploremap`
3. The whole map will be revealed, however it takes a while for it to fully generate, about 2 minutes on my PC.
4. To rehide the map you can enter `resetmap`
5. To free fly enter `debugmode`, close console, and then press `z`
6. To immediately teleport to a location on the map you can enter `goto <coords>`, where coords are in the range
`-10000` to `10000` with the origin at the center, and North and East are positive.
    
    e.g. `goto 5000 -5000` will be a location to the South-East of the map center, about half way to the edge.

---
## Possible Future Development

- DONE ~~Allow pre-mod worlds to still work correctly~~
- DONE ~~Expose parameters via configuration file~~
- DONE ~~Allow use of image as base height map~~
- Determine a set of parameter presets for different effects
- Integrate FastNoiseLite to introduce alternative noise types for variation
- Integrate a base heightmap pre-gen step to allow more time consuming or holistic techniques (e.g. erosion, tectonics, other physical processes etc.)
- Calculate biomes using results from pre-gen physical modeling / simulation. e.g. use deposition to determine plains, erosion to determine mountains etc.