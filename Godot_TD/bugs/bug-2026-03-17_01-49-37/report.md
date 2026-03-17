# Bug Report: It partially cleans up the lines. Closer but not there ye...
**Date:** 2026-03-17 01:49:37
**Severity:** medium
**Highlighted Region:** [13, 12, 1898, 1060]

## Description
It partially cleans up the lines. Closer but not there yet.

Godot Engine v4.6.1.stable.mono.official.14d19694e - https://godotengine.org
Vulkan 1.4.325 - Forward+ - Using Device #0: NVIDIA - NVIDIA GeForce RTX 5080

[GameManager] Phase: Boot -> MainMenu
[Cinematic] Ambient drone started
[VineDraft] Selected role: Bruteforge with 8 nodes
[VineBattle] _Ready START
[VineBattle] Creating grid...
[VineBattle] Building map layout...
[VineBattle] Creating pathfinder...
[VineBattle] Creating wave manager...
[VineBattle] Creating placer...
[VineBattle] Creating path preview...
[VineBattle] Creating camera...
[VineBattle] Setting up lighting...
[VineBattle] Setting up environment...
[VineBattle] Building environment dressing...
[VineBattle]   Outer grid lines...
[VineBattle]   Cliff ring...
[VineBattle]   Background structures...
[VineBattle]   Background pillars...
[VineBattle]   Tron fog...
--Main Shader--
   15 | 
   16 | void fragment() {
E  17->  float phase = INSTANCE_CUSTOM.x;
   18 |  float speed = INSTANCE_CUSTOM.y;
   19 |  float wave = sin(TIME * speed + phase) * 0.5 + 0.5;
[VineBattle]   Horizon silhouettes...
[VineBattle] Environment dressing complete.
[VineBattle] Creating HUD...
[VineBattle] Creating AXIS commentary...
[GameManager] Phase: MainMenu -> Build
[VineBattle] _Ready COMPLETE
[Sandbox] Repeater Tower: visible size=0.6x3.0x0.6, normScale=0.5
[Sandbox] Applying theme: faction=0, outlineMode=0
[TronTheme] ApplyWithMode: outline=0 (Per-Mesh Outline), color=(0.00,0.80,0.90)
[Sandbox] Applying theme: faction=0, outlineMode=1
[TronTheme] ApplyWithMode: outline=1 (Silhouette Only), color=(0.00,0.80,0.90)
[TronTheme] Silhouette clone: 1 meshes (original had 1)
[Sandbox] Applying theme: faction=0, outlineMode=1
[TronTheme] ApplyWithMode: outline=1 (Silhouette Only), color=(0.00,0.80,0.90)
[TronTheme] Silhouette clone: 1 meshes (original had 1)
[Sandbox] Applying theme: faction=0, outlineMode=1
[TronTheme] ApplyWithMode: outline=1 (Silhouette Only), color=(0.00,0.80,0.90)
[TronTheme] Silhouette clone: 1 meshes (original had 1)
[Sandbox] Applying theme: faction=0, outlineMode=2
[TronTheme] ApplyWithMode: outline=2 (No Outline), color=(0.00,0.80,0.90)
[Sandbox] Applying theme: faction=0, outlineMode=2
[TronTheme] ApplyWithMode: outline=2 (No Outline), color=(0.00,0.80,0.90)
[Sandbox] Applying theme: faction=0, outlineMode=2
[TronTheme] ApplyWithMode: outline=2 (No Outline), color=(0.00,0.80,0.90)
[Sandbox] Applying theme: faction=0, outlineMode=2
[TronTheme] ApplyWithMode: outline=2 (No Outline), color=(0.00,0.80,0.90)

## Screenshot
![screenshot](screenshot.png)

## Metrics
- FPS: 240
- Phase: Build
- Wave: 0 / 6
- Gold: 80
- Lives: 10
- Enemies alive: 0
- Nodes placed: 0
- Mode: Vine Logic TD
