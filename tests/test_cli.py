from __future__ import annotations

import pytest
from unittest.mock import patch
from cmdai.cli import main


def test_cli_empty_args_shows_help() -> None:
    # Running main with empty args should trigger argparse --help which raises SystemExit(0)
    with patch("sys.argv", ["cmdai"]):
        with pytest.raises(SystemExit) as excinfo:
            main()
        assert excinfo.value.code == 0
