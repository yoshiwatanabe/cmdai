from __future__ import annotations

import os
from pathlib import Path


def load_env_files() -> list[Path]:
    loaded: list[Path] = []
    for path in dict.fromkeys(_candidate_env_files()):
        if not path.exists() or not path.is_file():
            continue
        _load_env_file(path)
        loaded.append(path)
    return loaded


def _candidate_env_files() -> list[Path]:
    package_root = Path(__file__).resolve().parents[1]
    return [
        package_root / ".env",
        Path.cwd() / ".env",
        Path.home() / ".cmdai" / ".env",
    ]


def _load_env_file(path: Path) -> None:
    for line in path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        key = key.strip()
        value = _strip_quotes(value.strip())
        if key and key not in os.environ:
            os.environ[key] = value


def _strip_quotes(value: str) -> str:
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value
