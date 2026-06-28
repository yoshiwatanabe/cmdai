from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class ToolDoc:
    id: int
    tool: str
    shell: str
    source: str
    version: str | None
    content: str
    updated_at: str


@dataclass(frozen=True)
class SearchHit:
    doc: ToolDoc
    score: float
    snippet: str


@dataclass(frozen=True)
class HelpCapture:
    tool: str
    shell: str
    source: str
    version: str | None
    content: str


@dataclass(frozen=True)
class AskHistoryEntry:
    id: int
    query: str
    shell: str
    inferred_tool: str | None
    command: str | None
    source: str
    note: str | None
    created_at: str


@dataclass(frozen=True)
class Resolution:
    text: str
    query: str
    shell: str
    inferred_tool: str | None
    command: str | None
    source: str
    note: str | None


@dataclass(frozen=True)
class CommandCatalogEntry:
    name: str
    shell: str
    source: str
    synopsis: str | None
    syntax: str | None
    aliases: str | None
    updated_at: str
