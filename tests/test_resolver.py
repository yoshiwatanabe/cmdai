from __future__ import annotations

from pathlib import Path

from cmdai import resolver as resolver_module
from cmdai.models import CommandCatalogEntry, HelpCapture
from cmdai.resolver import infer_tool, resolve_query
from cmdai.storage import CacheStore



def test_infer_tool_from_cached_docs(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_doc(
            HelpCapture("robocopy", "powershell", "--help", None, "Robust File Copy for Windows")
        )
        assert infer_tool("use robocopy to mirror a directory", store) == "robocopy"
    finally:
        store.close()


def test_gemini_generation_is_used_before_fallback(tmp_path: Path, monkeypatch) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_command_catalog(
            [
                CommandCatalogEntry(
                    name="get-childitem",
                    shell="powershell",
                    source="Microsoft.PowerShell.Management",
                    synopsis="Gets the items and child items in one or more specified locations.",
                    syntax="Get-ChildItem [[-Path] <string[]>] [[-Filter] <string>] [-Recurse] [-File]",
                    aliases="gci,ls,dir",
                    updated_at="",
                )
            ]
        )
        store.upsert_doc(
            HelpCapture(
                "get-childitem",
                "powershell",
                "fixture",
                None,
                "Get-ChildItem [[-Path] <string[]>] [[-Filter] <string>] [-Recurse] [-File]",
            )
        )
        monkeypatch.setattr(
            resolver_module,
            "generate_with_gemini",
            lambda prompt: 'Command: Get-ChildItem -Path . -Recurse -File -Filter "*.pdf"',
        )

        output = resolve_query(
            store,
            "show all files that ends with .pdf in a directory recursively using PowerShell",
        )

        assert 'Command: Get-ChildItem -Path . -Recurse -File -Filter "*.pdf"' in output
    finally:
        store.close()


def test_unknown_gemini_generation_falls_back_after_inference(tmp_path: Path, monkeypatch) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_doc(
            HelpCapture(
                "git log",
                "powershell",
                "fixture",
                None,
                "git log supports -n <number> and --format with %an for author name.",
            )
        )
        monkeypatch.setattr(
            resolver_module,
            "generate_with_gemini",
            lambda prompt: "Command: unknown\nWhy: insufficient docs",
        )

        output = resolve_query(
            store,
            "show me the list of submitters for the last 5 commits",
        )

        assert 'No grounded command was generated.' in output
        assert "Gemini returned unknown command." in output
    finally:
        store.close()


def test_no_auto_groom_offers_likely_tool(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_command_catalog(
            [
                CommandCatalogEntry(
                    name="select-string",
                    shell="powershell",
                    source="Microsoft.PowerShell.Utility",
                    synopsis="Finds text in strings and files.",
                    syntax="Select-String [-Pattern] <string[]> [-Path] <string[]> [-CaseSensitive] [-SimpleMatch]",
                    aliases="sls",
                    updated_at="",
                )
            ]
        )
        output = resolve_query(
            store,
            "show occurances of exact word like foobar in case-insensitive manner and show line numbers using PowerShell",
            use_llm=False,
            auto_groom=False,
        )

        assert "Selected likely tool: select-string" in output or "Likely tools: select-string" in output
        assert 'groom "select-string"' in output
    finally:
        store.close()


def test_catalog_candidate_choice_can_offer_ungroomed_tool(tmp_path: Path, monkeypatch) -> None:
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
                )
            ]
        )
        monkeypatch.setattr(
            resolver_module,
            "choose_command_candidates",
            lambda query, command_names, limit=5: ["select-string"],
        )

        output = resolve_query(
            store,
            "show occurances of exact word like foobar in case-insensitive manner and show line numbers using PowerShell",
            use_llm=False,
            auto_groom=False,
        )

        assert "Likely tools: select-string" in output
        assert 'groom "select-string"' in output
    finally:
        store.close()


def test_resolve_without_llm_returns_retrieval(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_doc(
            HelpCapture(
                "git",
                "powershell",
                "--help",
                None,
                "git remote [-v | --verbose]\nManage set of tracked repositories.",
            )
        )
        output = resolve_query(store, "with git show remote urls", use_llm=False)
        assert "No grounded command was generated." in output
        assert "git" in output
    finally:
        store.close()


def test_resolve_selects_powershell_file_tool_without_tool_argument(tmp_path: Path) -> None:
    store = CacheStore(tmp_path / "cache.sqlite3")
    try:
        store.upsert_command_catalog(
            [
                CommandCatalogEntry(
                    name="get-childitem",
                    shell="powershell",
                    source="Microsoft.PowerShell.Management",
                    synopsis="Gets the items and child items in one or more specified locations.",
                    syntax="Get-ChildItem [[-Path] <string[]>] [[-Filter] <string>] [-Recurse] [-File]",
                    aliases="gci,ls,dir",
                    updated_at="",
                )
            ]
        )
        store.upsert_doc(
            HelpCapture(
                "get-childitem",
                "powershell",
                "fixture",
                None,
                "Get-ChildItem [[-Path] <string[]>] [[-Filter] <string>] [-Recurse] [-File]",
            )
        )
        output = resolve_query(
            store,
            "show all files that ends with .pdf in a directory recursively using PowerShell",
            use_llm=False,
        )
        assert "Selected likely tool: get-childitem" in output
        assert "get-childitem" in output
    finally:
        store.close()
