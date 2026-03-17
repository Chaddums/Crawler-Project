# Vine Logic TD — Claude Instructions

## Project

Vine Logic TD — a roguelike tower defense where the player builds a programmable logic network (sensors → signals → effects). The network IS the maze. Godot 4.6 C#, namespace `JunkyardTD`.

## Current Goal

**Ship the jam alpha.** Read `ALPHA_ROADMAP.md` for the definitive checklist of what's in scope, what's done, and what's cut. Do not implement anything marked `CUT` unless the user explicitly asks.

## Architecture

- Code-built UI (no .tscn UI scenes) — all UI extends CanvasLayer and builds controls in `_Ready()`
- `TronTheme.cs` for all colors/materials — dark blue-black with cyan emissive grid lines
- `ServiceLocator` for singletons, `GameEvents` static event bus
- `VineNodeRegistry` for node type definitions, `VineWaveRegistry` for wave data
- Procedural meshes for enemies/towers (no skeletal animation)
- PCM audio synthesis for placeholder sounds (see `IntroCinematic.cs` for patterns)

## Conventions

- Namespace: `JunkyardTD`
- Scene files need `[ext_resource]` blocks with `load_steps` (see existing .tscn files for format)
- Node types defined in `Enums.cs`, data in `VineNodeData.cs`
- Game flow: MainMenu → IntroCinematic → VineDraft → VineBattle
- Constants in `Constants.cs`, not magic numbers in code
