#!/usr/bin/env node
// Batch convert game-icons.net SVGs to 64x64 PNGs with junkbot color palette
// Colors: foreground tinted per category, dark background circle

import sharp from 'sharp';
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync } from 'fs';
import { join, dirname } from 'path';

const REPO = '/mnt/c/Users/Stu/GitHub/Crawler_Project/Godot/_downloads/icons/game-icons-net';
const OUT = '/mnt/c/Users/Stu/GitHub/Crawler_Project/Godot/Icons';
const SIZE = 64;

// Junkbot palette — foreground colors per category
const PALETTE = {
  abilities:   { fg: '#FF8844', bg: '#1A1A2E' },  // rusted orange on dark
  weapons:     { fg: '#CC3333', bg: '#1A1A2E' },  // red on dark
  armor:       { fg: '#7799BB', bg: '#1A1A2E' },  // steel blue on dark
  consumables: { fg: '#44DD88', bg: '#1A1A2E' },  // green on dark
  botframes:   { fg: '#DDAA33', bg: '#1A1A2E' },  // gold on dark
  enemies:     { fg: '#DD4444', bg: '#1A1A2E' },  // red on dark
  companions:  { fg: '#44BBDD', bg: '#1A1A2E' },  // cyan on dark
  lootboxes:   { fg: '#DDBB44', bg: '#1A1A2E' },  // gold on dark
  rarity:      { fg: '#BBBBBB', bg: '#1A1A2E' },  // silver on dark
  stats:       { fg: '#88CCFF', bg: '#1A1A2E' },  // light blue on dark
};

// Icon mapping from ICON_MAPPING.md
const MAPPINGS = [
  // ABILITIES
  { src: 'lorc/punch-blast.svg', dst: 'Abilities/ability_strike.png', cat: 'abilities' },
  { src: 'delapouite/shield-bash.svg', dst: 'Abilities/ability_shield_bash.png', cat: 'abilities' },
  { src: 'lorc/tornado-discs.svg', dst: 'Abilities/ability_whirlwind.png', cat: 'abilities' },
  { src: 'lorc/quake-stomp.svg', dst: 'Abilities/ability_slam.png', cat: 'abilities' },
  { src: 'lorc/sonic-shout.svg', dst: 'Abilities/ability_feral_roar.png', cat: 'abilities' },
  { src: 'lorc/meteor-impact.svg', dst: 'Abilities/ability_earthquake.png', cat: 'abilities' },
  { src: 'lorc/plasma-bolt.svg', dst: 'Abilities/ability_arcane_bolt.png', cat: 'abilities' },
  { src: 'delapouite/cryo-chamber.svg', dst: 'Abilities/ability_frost_nova.png', cat: 'abilities' },
  { src: 'lorc/burning-meteor.svg', dst: 'Abilities/ability_meteor.png', cat: 'abilities' },
  { src: 'lorc/backstab.svg', dst: 'Abilities/ability_backstab.png', cat: 'abilities' },
  { src: 'darkzaitzev/smoke-bomb.svg', dst: 'Abilities/ability_smoke_bomb.png', cat: 'abilities' },
  { src: 'lorc/dead-eye.svg', dst: 'Abilities/ability_assassinate.png', cat: 'abilities' },
  { src: 'lorc/sonic-lightning.svg', dst: 'Abilities/ability_dark_chord.png', cat: 'abilities' },
  { src: 'lorc/auto-repair.svg', dst: 'Abilities/ability_raise_dead.png', cat: 'abilities' },
  { src: 'skoll/sound-waves.svg', dst: 'Abilities/ability_death_ballad.png', cat: 'abilities' },
  { src: 'lorc/spinning-blades.svg', dst: 'Abilities/ability_flurry.png', cat: 'abilities' },
  { src: 'lorc/fulguro-punch.svg', dst: 'Abilities/ability_uppercut.png', cat: 'abilities' },
  { src: 'lorc/mailed-fist.svg', dst: 'Abilities/ability_hundred_fists.png', cat: 'abilities' },
  { src: 'lorc/ion-cannon-blast.svg', dst: 'Abilities/ability_cannon_blast.png', cat: 'abilities' },
  { src: 'skoll/laser-burst.svg', dst: 'Abilities/ability_burst_fire.png', cat: 'abilities' },
  { src: 'lorc/laser-precision.svg', dst: 'Abilities/ability_snipe_shot.png', cat: 'abilities' },
  { src: 'lorc/cogsplosion.svg', dst: 'Abilities/ability_rivet_burst.png', cat: 'abilities' },

  // WEAPONS
  { src: 'lorc/energy-sword.svg', dst: 'Items/Equipment/sword.png', cat: 'weapons' },
  { src: 'lorc/bolt-saw.svg', dst: 'Items/Equipment/dagger.png', cat: 'weapons' },
  { src: 'lorc/tesla-coil.svg', dst: 'Items/Equipment/staff.png', cat: 'weapons' },
  { src: 'john-colburn/pistol-gun.svg', dst: 'Items/Equipment/pistol.png', cat: 'weapons' },
  { src: 'sbed/rifle.svg', dst: 'Items/Equipment/rifle.png', cat: 'weapons' },
  { src: 'sbed/shotgun.svg', dst: 'Items/Equipment/shotgun.png', cat: 'weapons' },
  { src: 'delapouite/missile-launcher.svg', dst: 'Items/Equipment/launcher.png', cat: 'weapons' },
  { src: 'lorc/autogun.svg', dst: 'Items/Equipment/repeater.png', cat: 'weapons' },
  { src: 'lorc/bolt-shield.svg', dst: 'Items/Equipment/shield.png', cat: 'weapons' },
  { src: 'sbed/circular-saw.svg', dst: 'Items/Equipment/ring.png', cat: 'weapons' },

  // ARMOR
  { src: 'delapouite/robot-helmet.svg', dst: 'Items/Equipment/helmet.png', cat: 'armor' },
  { src: 'delapouite/chest-armor.svg', dst: 'Items/Equipment/chestplate.png', cat: 'armor' },
  { src: 'lorc/armor-vest.svg', dst: 'Items/Equipment/robe.png', cat: 'armor' },
  { src: 'delapouite/greaves.svg', dst: 'Items/Equipment/greaves.png', cat: 'armor' },
  { src: 'delapouite/metal-boot.svg', dst: 'Items/Equipment/boots.png', cat: 'armor' },
  { src: 'lorc/mechanical-arm.svg', dst: 'Items/Equipment/gauntlets.png', cat: 'armor' },
  { src: 'delapouite/steam.svg', dst: 'Items/Equipment/cloak.png', cat: 'armor' },
  { src: 'lorc/aerial-signal.svg', dst: 'Items/Equipment/amulet.png', cat: 'armor' },

  // CONSUMABLES
  { src: 'sbed/battery-pack.svg', dst: 'Items/Consumables/potion_health_small.png', cat: 'consumables' },
  { src: 'sbed/battery-pack-alt.svg', dst: 'Items/Consumables/potion_health_medium.png', cat: 'consumables' },
  { src: 'priorblue/battery-100.svg', dst: 'Items/Consumables/potion_health_large.png', cat: 'consumables' },
  { src: 'priorblue/battery-25.svg', dst: 'Items/Consumables/potion_mana_small.png', cat: 'consumables' },
  { src: 'priorblue/battery-50.svg', dst: 'Items/Consumables/potion_mana_medium.png', cat: 'consumables' },
  { src: 'priorblue/battery-75.svg', dst: 'Items/Consumables/potion_mana_large.png', cat: 'consumables' },
  { src: 'lorc/energy-shield.svg', dst: 'Items/Consumables/elixir_fortitude.png', cat: 'consumables' },
  { src: 'lorc/overdrive.svg', dst: 'Items/Consumables/overclock_injector.png', cat: 'consumables' },

  // BOT FRAMES
  { src: 'delapouite/battle-mech.svg', dst: 'BotFrames/scrapheap.png', cat: 'botframes' },
  { src: 'lorc/robot-golem.svg', dst: 'BotFrames/tincan.png', cat: 'botframes' },
  { src: 'sbed/tesla.svg', dst: 'BotFrames/sparkplug.png', cat: 'botframes' },
  { src: 'lorc/vintage-robot.svg', dst: 'BotFrames/rustbucket.png', cat: 'botframes' },
  { src: 'delapouite/speaker.svg', dst: 'BotFrames/noisebox.png', cat: 'botframes' },
  { src: 'delapouite/mecha-head.svg', dst: 'BotFrames/clunker.png', cat: 'botframes' },

  // ENEMIES
  { src: 'lorc/target-dummy.svg', dst: 'Enemies/calibration_target.png', cat: 'enemies' },
  { src: 'delapouite/spider-bot.svg', dst: 'Enemies/scrap_rat.png', cat: 'enemies' },
  { src: 'lorc/target-shot.svg', dst: 'Enemies/decoy_unit.png', cat: 'enemies' },
  { src: 'lorc/centipede.svg', dst: 'Enemies/wire_worm.png', cat: 'enemies' },
  { src: 'lorc/sentry-gun.svg', dst: 'Enemies/Bosses/corrupted_sentry.png', cat: 'enemies' },
  { src: 'lorc/hydra.svg', dst: 'Enemies/Bosses/scrap_hydra.png', cat: 'enemies' },
  { src: 'delapouite/mecha-mask.svg', dst: 'Enemies/Bosses/axis_avatar.png', cat: 'enemies' },

  // COMPANIONS
  { src: 'delapouite/delivery-drone.svg', dst: 'Companions/bit.png', cat: 'companions' },

  // LOOT BOXES
  { src: 'delapouite/cardboard-box-closed.svg', dst: 'LootBoxes/bronze.png', cat: 'lootboxes' },
  { src: 'delapouite/locked-box.svg', dst: 'LootBoxes/silver.png', cat: 'lootboxes' },
  { src: 'delapouite/strongbox.svg', dst: 'LootBoxes/gold.png', cat: 'lootboxes' },
  { src: 'delapouite/chest.svg', dst: 'LootBoxes/diamond.png', cat: 'lootboxes' },
  { src: 'sbed/ammo-box.svg', dst: 'LootBoxes/legendary.png', cat: 'lootboxes' },

  // RARITY
  { src: 'lorc/cog.svg', dst: 'UI/Rarity/common.png', cat: 'rarity' },
  { src: 'lorc/gears.svg', dst: 'UI/Rarity/uncommon.png', cat: 'rarity' },
  { src: 'delapouite/cogsplosion.svg', dst: 'UI/Rarity/rare.png', cat: 'rarity' },
  { src: 'lorc/circuitry.svg', dst: 'UI/Rarity/epic.png', cat: 'rarity' },
  { src: 'lorc/processor.svg', dst: 'UI/Rarity/legendary.png', cat: 'rarity' },
  { src: 'lorc/microchip.svg', dst: 'UI/Rarity/absurd.png', cat: 'rarity' },

  // STATS
  { src: 'lorc/gear-hammer.svg', dst: 'UI/Stats/strength.png', cat: 'stats' },
  { src: 'delapouite/speedometer.svg', dst: 'UI/Stats/dexterity.png', cat: 'stats' },
  { src: 'delapouite/cpu.svg', dst: 'UI/Stats/intelligence.png', cat: 'stats' },
  { src: 'lorc/shield-reflect.svg', dst: 'UI/Stats/constitution.png', cat: 'stats' },
  { src: 'delapouite/robot-antennas.svg', dst: 'UI/Stats/charisma.png', cat: 'stats' },
  { src: 'delapouite/horseshoe.svg', dst: 'UI/Stats/luck.png', cat: 'stats' },
];

function findSvg(relPath) {
  // game-icons.net repo structure: artist/icon-name.svg
  // But some may be in subdirectories. Let's try direct path first.
  const direct = join(REPO, relPath);
  if (existsSync(direct)) return direct;

  // Try searching by filename in all artist dirs
  const filename = relPath.split('/').pop();
  const artists = readdirSync(REPO).filter(d => {
    try { return existsSync(join(REPO, d)) && !d.startsWith('.'); } catch { return false; }
  });
  for (const artist of artists) {
    const candidate = join(REPO, artist, filename);
    if (existsSync(candidate)) return candidate;
  }
  return null;
}

async function convertIcon(mapping) {
  const { src, dst, cat } = mapping;
  const palette = PALETTE[cat] || PALETTE.abilities;

  const svgPath = findSvg(src);
  if (!svgPath) {
    console.error(`MISSING: ${src} -> ${dst}`);
    return false;
  }

  let svgContent = readFileSync(svgPath, 'utf-8');

  // Replace white fill with our foreground color
  // game-icons.net SVGs use fill="#fff" or fill="#000" on a background
  // We want: colored icon on dark circular background
  svgContent = svgContent.replace(/fill="#fff"/g, `fill="${palette.fg}"`);
  svgContent = svgContent.replace(/fill="white"/g, `fill="${palette.fg}"`);
  svgContent = svgContent.replace(/fill="#000"/g, `fill="${palette.bg}"`);
  svgContent = svgContent.replace(/fill="black"/g, `fill="${palette.bg}"`);

  const outPath = join(OUT, dst);
  mkdirSync(dirname(outPath), { recursive: true });

  try {
    await sharp(Buffer.from(svgContent))
      .resize(SIZE, SIZE)
      .png()
      .toFile(outPath);
    return true;
  } catch (e) {
    console.error(`ERROR converting ${src}: ${e.message}`);
    return false;
  }
}

async function main() {
  let success = 0, fail = 0;

  for (const mapping of MAPPINGS) {
    const ok = await convertIcon(mapping);
    if (ok) {
      success++;
      process.stdout.write('.');
    } else {
      fail++;
    }
  }

  console.log(`\n\nDone: ${success} converted, ${fail} failed out of ${MAPPINGS.length} total`);
}

main();
