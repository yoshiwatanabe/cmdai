from __future__ import annotations

from pathlib import Path

from cmdai_next.models import CommandCatalogEntry, HelpCapture
from cmdai_next.storage import CacheStore


def test_upsert_and_search(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_doc(
            HelpCapture(
                tool="grep",
                shell="bash",
                source="--help",
                version="grep 1.0",
                content="Usage: grep [OPTION] PATTERNS [FILE]\n-r, --recursive read all files under each directory",
            )
        )
        hits = store.search_any("search recursively", tool="grep", shell="bash")
        assert hits
        assert hits[0].doc.tool == "grep"
        assert "recursive" in hits[0].doc.content
    finally:
        store.close()


def test_search_handles_file_extension_punctuation(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_doc(
            HelpCapture(
                tool="get-childitem",
                shell="powershell",
                source="fixture",
                version=None,
                content="Get-ChildItem supports -Recurse, -File, and -Filter for matching files.",
            )
        )
        hits = store.search_any("show all .pdf files recursively", shell="powershell")
        assert hits
    finally:
        store.close()


def test_ask_history_and_usage_stats(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.record_ask(
            query="show pdf files",
            shell="powershell",
            inferred_tool="get-childitem",
            command='Get-ChildItem -Path "." -Recurse -File -Filter *.pdf',
            source="gemini",
            note=None,
        )
        store.record_ask(
            query="list subscriptions",
            shell="powershell",
            inferred_tool="az account",
            command="az account list",
            source="gemini",
            note=None,
        )

        history = store.list_ask_history()
        assert len(history) == 2
        assert store.usage_by_tool() == [("az account", 1), ("get-childitem", 1)]
        assert store.usage_by_source() == [("gemini", 2)]
        assert ("az account list", 1) in store.usage_by_command()
    finally:
        store.close()


def test_command_catalog_helpers(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_command_catalog(
            [
                CommandCatalogEntry(
                    name="select-string",
                    shell="powershell",
                    source="Microsoft.PowerShell.Utility",
                    synopsis="Select-String",
                    syntax="",
                    aliases="sls",
                    updated_at="",
                ),
                CommandCatalogEntry(
                    name="new-azvm",
                    shell="powershell",
                    source="Az.Compute",
                    synopsis="New-AzVM",
                    syntax="",
                    aliases="",
                    updated_at="",
                )
            ]
        )

        assert "select-string" in store.catalog_names("powershell")
        assert "new-azvm" in store.catalog_names("powershell")

        # Test deleting by source module
        store.delete_catalog_by_source("powershell", "Az.Compute")
        names = store.catalog_names("powershell")
        assert "select-string" in names
        assert "new-azvm" not in names
    finally:
        store.close()

