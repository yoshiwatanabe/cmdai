from __future__ import annotations

import json
from pathlib import Path
import re
from dataclasses import dataclass


@dataclass(frozen=True)
class ToolHint:
    tool: str
    shell: str
    score: int
    reason: str


def infer_tool_hints(query: str, shell: str) -> list[ToolHint]:
    return infer_general_tool_hints(query)


def _load_hints() -> dict[str, list[str]]:
    package_json = Path(__file__).parent / "data" / "hints.json"
    user_json = Path.home() / ".cmdai" / "hints.json"
    
    hints: dict[str, list[str]] = {}
    
    # Load defaults
    if package_json.exists():
        try:
            with open(package_json, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, dict):
                    for k, v in data.items():
                        if isinstance(v, list):
                            hints[k] = [str(x) for x in v]
        except Exception:
            pass
            
    # Load user customized overrides/additions
    if user_json.exists():
        try:
            with open(user_json, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, dict):
                    for k, v in data.items():
                        if isinstance(v, list):
                            hints[k] = [str(x) for x in v]
        except Exception:
            pass
            
    return hints


def infer_general_tool_hints(query: str) -> list[ToolHint]:
    lowered = query.lower()
    hints: list[ToolHint] = []
    
    general_hints = _load_hints()
    for tool, phrases in general_hints.items():
        matched = [phrase for phrase in phrases if _matches(lowered, phrase)]
        if not matched:
            continue
        score = sum(3 if " " in phrase else 1 for phrase in matched)
        if " " in tool:
            score += 3
        hints.append(ToolHint(tool=tool, shell="any", score=score, reason=", ".join(matched[:5])))
    return sorted(hints, key=lambda hint: hint.score, reverse=True)


def _matches(query: str, phrase: str) -> bool:
    if " " in phrase:
        return phrase in query
    return re.search(rf"(^|\W){re.escape(phrase)}($|\W)", query) is not None

