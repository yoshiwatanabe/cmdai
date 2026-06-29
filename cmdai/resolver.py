from __future__ import annotations

import re

from .collector import capture_tool_help
from .llm import (
    LlmUnavailable,
    choose_catalog_candidates,
    choose_command_candidates,
    generate_with_gemini,
    suggest_catalog_search_terms,
)
from .models import Resolution, SearchHit
from .storage import CacheStore
from .tool_hints import ToolHint, infer_tool_hints


KNOWN_TOOLS = {
    "git",
    "grep",
    "find",
    "rg",
    "az",
    "docker",
    "kubectl",
    "npm",
    "yarn",
    "pwsh",
    "powershell",
    "where",
    "where.exe",
    "xcopy",
    "robocopy",
}


def resolve_query(
    store: CacheStore,
    query: str,
    tool: str | None = None,
    shell: str = "powershell",
    use_llm: bool = True,
    auto_groom: bool = True,
) -> str:
    return resolve_query_detail(store, query, tool, shell, use_llm, auto_groom).text


def resolve_query_detail(
    store: CacheStore,
    query: str,
    tool: str | None = None,
    shell: str = "powershell",
    use_llm: bool = True,
    auto_groom: bool = True,
) -> Resolution:
    hinted_tools = infer_tool_hints(query, shell)
    catalog_hints = _catalog_hints(store, query, shell)
    hinted_tools = _merge_hints(catalog_hints, hinted_tools)
    inferred = infer_tool(query, store)
    if inferred and inferred in {"powershell", "pwsh", "ps"}:
        inferred = None
    effective_tool = tool or inferred or (hinted_tools[0].tool if hinted_tools else None)
    effective_tool = effective_tool.lower() if effective_tool else None
    grooming_note: str | None = None
    if auto_groom and effective_tool and not store.docs_for_tool(effective_tool, shell=shell, limit=1):
        captures = capture_tool_help(effective_tool, shell=shell)
        if captures:
            store.replace_docs(effective_tool, shell.lower(), captures)
            grooming_note = f"auto-groomed {effective_tool}"
        else:
            grooming_note = f"attempted {effective_tool}, no useful help captured"
    elif not auto_groom and effective_tool and not store.docs_for_tool(effective_tool, shell=shell, limit=1):
        grooming_note = f"not groomed; run: .\\cmdai-next.ps1 groom \"{effective_tool}\" --shell {shell}"

    hits = store.search_any(query, tool=effective_tool, shell=shell, limit=6) if effective_tool else store.search_any(query, shell=shell, limit=6)
    should_offer_grooming = bool(grooming_note and grooming_note.startswith("not groomed;"))
    if not hits and effective_tool and not should_offer_grooming:
        hits = store.search_any(query, shell=shell, limit=6)
    if not hits and effective_tool and not should_offer_grooming:
        hits = _fallback_docs(store, effective_tool, shell)

    if not hits:
        target = f" for {effective_tool}" if effective_tool else ""
        hint_text = _format_hints(hinted_tools)
        groom = f'.\\cmdai-next.ps1 groom "{hinted_tools[0].tool}" --shell {shell}' if hinted_tools else ".\\cmdai-next.ps1 groom <tool>"
        text = f"No cached documentation found{target}.{hint_text}\nRun: {groom}"
        text = _with_grooming_note(text, grooming_note)
        return Resolution(text, query, shell, effective_tool, None, "no_docs", "no cached documentation")

    prompt = build_prompt(query, shell, hits)
    if use_llm:
        try:
            generated = generate_with_gemini(prompt)
            if generated:
                if _is_unknown_generation(generated):
                    text = format_retrieval_only(query, hits, "Gemini returned unknown command.", hinted_tools, grooming_note)
                    return Resolution(text, query, shell, effective_tool, None, "retrieval_after_unknown", "Gemini returned unknown")
                return Resolution(
                    _with_grooming_note(generated, grooming_note),
                    query,
                    shell,
                    effective_tool,
                    _extract_generated_command(generated),
                    "gemini",
                    None,
                )
        except LlmUnavailable as exc:
            text = format_retrieval_only(query, hits, str(exc), hinted_tools, grooming_note)
            return Resolution(text, query, shell, effective_tool, None, "retrieval_after_llm_error", str(exc))

    text = format_retrieval_only(query, hits, "LLM generation disabled.", hinted_tools, grooming_note)
    return Resolution(text, query, shell, effective_tool, None, "retrieval_no_ai", "LLM generation disabled")


def infer_tool(query: str, store: CacheStore) -> str | None:
    lowered = query.lower()
    tools = [tool for tool, _, _, _ in store.list_tools()]
    for tool in sorted(set(tools), key=len, reverse=True):
        parts = re.findall(r"[a-z0-9_.-]+", tool.lower())
        if len(parts) > 1 and all(re.search(rf"(^|\W){re.escape(part)}($|\W)", lowered) for part in parts):
            return _normalize_tool(tool)
    for tool in sorted(set(tools), key=len, reverse=True):
        if re.search(rf"(^|\W){re.escape(tool.lower())}($|\W)", lowered):
            return _normalize_tool(tool)
    for tool in sorted(KNOWN_TOOLS, key=len, reverse=True):
        if re.search(rf"(^|\W){re.escape(tool)}($|\W)", lowered):
            return _normalize_tool(tool)
    return None


def build_prompt(query: str, shell: str, hits: list[SearchHit]) -> str:
    docs = []
    for index, hit in enumerate(hits, start=1):
        content = _clip(hit.doc.content, 2600)
        docs.append(
            f"[doc {index}] tool={hit.doc.tool} shell={hit.doc.shell} source={hit.doc.source}\n{content}"
        )
    return f"""You generate command-line commands for {shell}.

Use only the cached tool documentation below. If the documentation is insufficient, say what is missing.
Return:
Command: <single command or 'unknown'>
Why: <one short explanation>
Docs: <doc numbers used>

Request: {query}

Cached documentation:
{chr(10).join(docs)}
"""


def format_retrieval_only(
    query: str,
    hits: list[SearchHit],
    note: str,
    hinted_tools: list[ToolHint] | None = None,
    grooming_note: str | None = None,
) -> str:
    lines = [
        "No grounded command was generated.",
        f"Note: {note}",
        f"Query: {query}",
    ]
    if hinted_tools:
        lines.append(f"Selected likely tool: {hinted_tools[0].tool} ({hinted_tools[0].reason})")
    if grooming_note:
        lines.append(f"Grooming: {grooming_note}")
    lines.extend(["", "Most relevant cached docs:"])
    for index, hit in enumerate(hits, start=1):
        lines.append(f"{index}. {hit.doc.tool} [{hit.doc.shell}, {hit.doc.source}, {hit.doc.updated_at}]")
        lines.append(f"   {hit.snippet or _first_line(hit.doc.content)}")
    return "\n".join(lines)


def _fallback_docs(store: CacheStore, tool: str, shell: str) -> list[SearchHit]:
    docs = store.docs_for_tool(tool, shell=shell, limit=3) or store.docs_for_tool(tool, limit=3)
    return [SearchHit(doc=doc, score=0.0, snippet=_first_line(doc.content)) for doc in docs]


def _format_hints(hinted_tools: list[ToolHint]) -> str:
    if not hinted_tools:
        return ""
    top = ", ".join(f"{hint.tool} ({hint.reason})" for hint in hinted_tools[:3])
    return f"\nLikely tools: {top}."


def _catalog_hints(store: CacheStore, query: str, shell: str) -> list[ToolHint]:
    if shell.lower() not in {"powershell", "pwsh", "ps"}:
        return []
    direct_entries = [
        entry for entry in store.search_catalog(query, "powershell", limit=30)
        if "-" in entry.name and len(entry.name) > 3
    ]
    expanded_entries = []
    for term in suggest_catalog_search_terms(query):
        for entry in store.search_catalog(term, "powershell", limit=10):
            if "-" in entry.name and len(entry.name) > 3 and all(existing.name != entry.name for existing in expanded_entries):
                expanded_entries.append(entry)
    lexical_entries = _dedupe_catalog_entries(expanded_entries + direct_entries)
    candidate_pairs = _catalog_candidate_summaries(lexical_entries[:12], shell)
    names = choose_catalog_candidates(query, candidate_pairs, limit=5)
    entries = [entry for name in names if (entry := store.catalog_entry(name, "powershell")) is not None]
    if not entries:
        catalog_names = [
            name for name in store.catalog_names("powershell", limit=20000)
            if "-" in name and len(name) > 3
        ]
        names = choose_command_candidates(query, catalog_names, limit=5)
        entries = [entry for name in names if (entry := store.catalog_entry(name, "powershell")) is not None]
    if not entries:
        entries = lexical_entries[:5]
    hints: list[ToolHint] = []
    for index, entry in enumerate(entries):
        reason_parts = []
        if entry.synopsis:
            reason_parts.append(entry.synopsis.splitlines()[0][:120])
        elif entry.syntax:
            reason_parts.append(entry.syntax.splitlines()[0][:120])
        reason = reason_parts[0] if reason_parts else "matched discovered command catalog"
        hints.append(ToolHint(entry.name, "powershell", 100 - index, reason))
    return hints


def _merge_hints(*hint_groups: list[ToolHint]) -> list[ToolHint]:
    merged: dict[str, ToolHint] = {}
    for hints in hint_groups:
        for hint in hints:
            existing = merged.get(hint.tool)
            if existing is None or hint.score > existing.score:
                merged[hint.tool] = hint
    return sorted(merged.values(), key=lambda hint: hint.score, reverse=True)


def _dedupe_catalog_entries(entries):
    result = []
    seen = set()
    for entry in entries:
        if entry.name in seen:
            continue
        seen.add(entry.name)
        result.append(entry)
    return result


def _catalog_candidate_summaries(entries, shell: str) -> list[tuple[str, str]]:
    pairs: list[tuple[str, str]] = []
    for entry in entries:
        parts = [part for part in [entry.synopsis, entry.syntax, entry.aliases] if part]
        pairs.append((entry.name, " ".join(parts)))
    return pairs


def _normalize_tool(tool: str) -> str:
    return "powershell" if tool in {"pwsh", "ps"} else tool


def _clip(value: str, limit: int) -> str:
    if len(value) <= limit:
        return value
    return value[:limit].rsplit("\n", 1)[0] + "\n..."


def _first_line(value: str) -> str:
    for line in value.splitlines():
        stripped = line.strip()
        if stripped:
            return stripped[:240]
    return ""


def _is_unknown_generation(value: str) -> bool:
    return any(
        line.strip().lower() in {"command: unknown", "command: `unknown`"}
        for line in value.splitlines()
    )


def _with_grooming_note(text: str, grooming_note: str | None) -> str:
    if not grooming_note:
        return text
    return f"Grooming: {grooming_note}\n{text}"


def _extract_generated_command(value: str) -> str | None:
    for line in value.splitlines():
        stripped = line.strip()
        if stripped.lower().startswith("command:"):
            command = stripped.split(":", 1)[1].strip().strip("`")
            return command if command and command.lower() != "unknown" else None
    return None
