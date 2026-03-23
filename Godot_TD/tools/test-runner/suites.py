"""
Suite name mapping for the Vine Logic TD test runner.

Each key resolves to one or more concrete suite names that the Godot
test harness understands.  Meta-suites like "all" and "quick" expand
to the appropriate list of concrete suites.
"""

SUITE_MAP: dict[str, list[str]] = {
    "all": ["content", "ui", "gameplay", "map-validation", "visual", "integration"],
    "content": ["content"],
    "ui": ["ui"],
    "gameplay": ["gameplay"],
    "visual": ["visual"],
    "integration": ["integration"],
    "map-validation": ["map-validation"],
    "maps": ["map-validation"],
    "quick": ["content", "ui"],
    "logic": ["content", "gameplay", "map-validation"],
}


def resolve(name: str) -> list[str]:
    """Resolve a suite name (possibly a meta-suite) to concrete suite names.

    Raises ``KeyError`` if *name* is not recognised.
    """
    if name not in SUITE_MAP:
        valid = ", ".join(sorted(SUITE_MAP.keys()))
        raise KeyError(f"Unknown suite '{name}'. Valid suites: {valid}")
    return SUITE_MAP[name]
