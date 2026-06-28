from __future__ import annotations

import os
from pathlib import Path

from cmdai_next.env_file import _load_env_file


def test_load_env_file_sets_missing_values(tmp_path: Path, monkeypatch) -> None:
    env_path = tmp_path / ".env"
    env_path.write_text(
        "\n".join(
            [
                "GEMINI_API_KEY='test-key'",
                'CMDAI_GEMINI_MODEL="gemini-3.5-flash"',
                "# ignored",
            ]
        ),
        encoding="utf-8",
    )
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.delenv("CMDAI_GEMINI_MODEL", raising=False)

    _load_env_file(env_path)

    assert os.environ["GEMINI_API_KEY"] == "test-key"
    assert os.environ["CMDAI_GEMINI_MODEL"] == "gemini-3.5-flash"
