from __future__ import annotations

import json
from pathlib import Path
from .models import HelpCapture


def _load_supplements() -> list[dict]:
    package_json = Path(__file__).parent / "data" / "supplements.json"
    user_json = Path.home() / ".cmdai" / "supplements.json"
    
    supplements: list[dict] = []
    
    # Load defaults
    if package_json.exists():
        try:
            with open(package_json, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list):
                    supplements.extend(data)
        except Exception:
            pass
            
    # Load user customizations
    if user_json.exists():
        try:
            with open(user_json, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list):
                    supplements.extend(data)
        except Exception:
            pass
            
    return supplements


def supplemental_docs(tool: str, shell: str, version: str | None) -> list[HelpCapture]:
    normalized = tool.lower()
    normalized_shell = shell.lower()
    
    results = []
    for item in _load_supplements():
        item_tool = item.get("tool", "").lower()
        if item_tool == normalized:
            item_shell = item.get("shell", "any").lower()
            if item_shell == "any" or item_shell == normalized_shell:
                results.append(
                    HelpCapture(
                        tool=normalized,
                        shell=normalized_shell,
                        source=item.get("source", f"cmdai supplement: {normalized}"),
                        version=version,
                        content=item.get("content", ""),
                    )
                )
    return results


