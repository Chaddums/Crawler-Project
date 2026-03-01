#!/bin/bash
# Downloads temp icons from game-icons.net (CC BY 3.0)
# White icons on transparent background, 512x512 PNG
# Run from the Godot/ directory: bash Tools/download_icons.sh

BASE="https://game-icons.net/icons/ffffff/transparent/1x1"
ICON_DIR="Icons"

download() {
    local designer="$1"
    local slug="$2"
    local output="$3"
    local url="$BASE/$designer/$slug.png"

    if [ -f "$output" ]; then
        echo "  SKIP $output (exists)"
        return
    fi

    curl -sL "$url" -o "$output"
    if [ $? -eq 0 ] && [ -s "$output" ]; then
        echo "  OK   $output"
    else
        echo "  FAIL $output ($url)"
        rm -f "$output"
    fi
}

echo "=== Downloading Ability Icons ==="
download lorc broadsword "$ICON_DIR/Abilities/ability_strike.png"
download lorc shield-bounces "$ICON_DIR/Abilities/ability_shield_bash.png"
download lorc spinning-sword "$ICON_DIR/Abilities/ability_whirlwind.png"
download lorc ram "$ICON_DIR/Abilities/ability_slam.png"
download lorc sonic-shout "$ICON_DIR/Abilities/ability_feral_roar.png"
download lorc quake-stomp "$ICON_DIR/Abilities/ability_earthquake.png"
download lorc bolt-eye "$ICON_DIR/Abilities/ability_arcane_bolt.png"
download lorc ice-bolt "$ICON_DIR/Abilities/ability_frost_nova.png"
download lorc meteor-impact "$ICON_DIR/Abilities/ability_meteor.png"
download lorc backstab "$ICON_DIR/Abilities/ability_backstab.png"
download lorc flash-grenade "$ICON_DIR/Abilities/ability_smoke_bomb.png"
download lorc dead-eye "$ICON_DIR/Abilities/ability_assassinate.png"
download lorc music-spell "$ICON_DIR/Abilities/ability_dark_chord.png"
download lorc spectre "$ICON_DIR/Abilities/ability_raise_dead.png"
download lorc death-note "$ICON_DIR/Abilities/ability_death_ballad.png"
download lorc punch-blast "$ICON_DIR/Abilities/ability_flurry.png"
download lorc fist "$ICON_DIR/Abilities/ability_uppercut.png"
download lorc mailed-fist "$ICON_DIR/Abilities/ability_hundred_fists.png"
download lorc cannon-ball "$ICON_DIR/Abilities/ability_cannon_blast.png"
download lorc strafe "$ICON_DIR/Abilities/ability_burst_fire.png"
download lorc targeting "$ICON_DIR/Abilities/ability_snipe_shot.png"
download lorc crowned-explosion "$ICON_DIR/Abilities/ability_rivet_burst.png"

echo ""
echo "=== Downloading Equipment Icons ==="
download lorc broadsword "$ICON_DIR/Items/Equipment/sword.png"
download lorc wizard-staff "$ICON_DIR/Items/Equipment/staff.png"
download lorc stiletto "$ICON_DIR/Items/Equipment/dagger.png"
download lorc cracked-shield "$ICON_DIR/Items/Equipment/shield.png"
download lorc crested-helmet "$ICON_DIR/Items/Equipment/helmet.png"
download delapouite chest-armor "$ICON_DIR/Items/Equipment/chestplate.png"
download lorc robe "$ICON_DIR/Items/Equipment/robe.png"
download delapouite greaves "$ICON_DIR/Items/Equipment/greaves.png"
download lorc boot-stomp "$ICON_DIR/Items/Equipment/boots.png"
download delapouite gauntlet "$ICON_DIR/Items/Equipment/gauntlets.png"
download lorc gem-pendant "$ICON_DIR/Items/Equipment/amulet.png"
download delapouite ring "$ICON_DIR/Items/Equipment/ring.png"
download delapouite cape "$ICON_DIR/Items/Equipment/cloak.png"
download john-colburn pistol-gun "$ICON_DIR/Items/Equipment/pistol.png"
download skoll lee-enfield "$ICON_DIR/Items/Equipment/rifle.png"
download delapouite sawed-off-shotgun "$ICON_DIR/Items/Equipment/shotgun.png"
download delapouite panzerfaust "$ICON_DIR/Items/Equipment/launcher.png"
download sbed chaingun "$ICON_DIR/Items/Equipment/repeater.png"

echo ""
echo "=== Downloading Consumable Icons ==="
download lorc potion-ball "$ICON_DIR/Items/Consumables/potion_health_small.png"
download lorc potion-ball "$ICON_DIR/Items/Consumables/potion_health_medium.png"
download lorc potion-ball "$ICON_DIR/Items/Consumables/potion_health_large.png"
download lorc double-ringed-orb "$ICON_DIR/Items/Consumables/potion_mana_small.png"
download lorc double-ringed-orb "$ICON_DIR/Items/Consumables/potion_mana_medium.png"
download lorc double-ringed-orb "$ICON_DIR/Items/Consumables/potion_mana_large.png"
download lorc shield-reflect "$ICON_DIR/Items/Consumables/elixir_fortitude.png"
download lorc syringe "$ICON_DIR/Items/Consumables/overclock_injector.png"

echo ""
echo "=== Downloading Stat Icons ==="
download lorc muscle-up "$ICON_DIR/UI/Stats/strength.png"
download lorc sprint "$ICON_DIR/UI/Stats/dexterity.png"
download lorc brain "$ICON_DIR/UI/Stats/intelligence.png"
download lorc crowned-heart "$ICON_DIR/UI/Stats/constitution.png"
download lorc charm "$ICON_DIR/UI/Stats/charisma.png"
download lorc clover "$ICON_DIR/UI/Stats/luck.png"

echo ""
echo "=== Downloading Rarity Icons ==="
download lorc cog "$ICON_DIR/UI/Rarity/common.png"
download lorc gears "$ICON_DIR/UI/Rarity/uncommon.png"
download lorc sheikah-eye "$ICON_DIR/UI/Rarity/rare.png"
download lorc crowned-skull "$ICON_DIR/UI/Rarity/epic.png"
download lorc crowned-explosion "$ICON_DIR/UI/Rarity/legendary.png"
download lorc bubble-field "$ICON_DIR/UI/Rarity/absurd.png"

echo ""
echo "=== Downloading Bot Frame Icons ==="
download lorc robot-golem "$ICON_DIR/BotFrames/scrapheap.png"
download lorc auto-repair "$ICON_DIR/BotFrames/tincan.png"
download lorc sinusoidal-beam "$ICON_DIR/BotFrames/sparkplug.png"
download delapouite spider-bot "$ICON_DIR/BotFrames/rustbucket.png"
download delapouite speaker "$ICON_DIR/BotFrames/noisebox.png"
download delapouite mecha-head "$ICON_DIR/BotFrames/clunker.png"

echo ""
echo "=== Downloading Enemy Icons ==="
download lorc target-dummy "$ICON_DIR/Enemies/calibration_target.png"
download delapouite rat "$ICON_DIR/Enemies/scrap_rat.png"
download lorc domino-mask "$ICON_DIR/Enemies/decoy_unit.png"
download lorc centipede "$ICON_DIR/Enemies/wire_worm.png"

echo ""
echo "=== Downloading Boss Icons ==="
download lorc sentry-gun "$ICON_DIR/Enemies/Bosses/corrupted_sentry.png"
download lorc hydra "$ICON_DIR/Enemies/Bosses/scrap_hydra.png"
download delapouite cctv-camera "$ICON_DIR/Enemies/Bosses/axis_avatar.png"

echo ""
echo "=== Downloading Companion Icons ==="
download delapouite companion-cube "$ICON_DIR/Companions/bit.png"

echo ""
echo "=== Downloading Loot Box Icons ==="
download lorc locked-chest "$ICON_DIR/LootBoxes/bronze.png"
download lorc locked-chest "$ICON_DIR/LootBoxes/silver.png"
download lorc locked-chest "$ICON_DIR/LootBoxes/gold.png"
download lorc locked-chest "$ICON_DIR/LootBoxes/diamond.png"
download lorc locked-chest "$ICON_DIR/LootBoxes/legendary.png"

echo ""
total=$(find "$ICON_DIR" -name "*.png" | wc -l)
echo "=== Done! $total icons downloaded ==="
echo "Icons are CC BY 3.0 from game-icons.net (Lorc, Delapouite, Skoll, sbed, john-colburn)"
