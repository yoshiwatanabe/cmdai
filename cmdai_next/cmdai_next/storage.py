from __future__ import annotations

import os
import re
import sqlite3
from pathlib import Path
from typing import Iterable

from .models import AskHistoryEntry, CommandCatalogEntry, HelpCapture, SearchHit, ToolDoc


def default_db_path() -> Path:
    configured = os.environ.get("CMDAI_DB")
    if configured:
        return Path(configured).expanduser()
    return Path.home() / ".cmdai" / "cmdai_next.sqlite3"


class CacheStore:
    def __init__(self, path: Path | None = None) -> None:
        self.path = path or default_db_path()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.connection = sqlite3.connect(self.path)
        self.connection.row_factory = sqlite3.Row
        self.initialize()

    def close(self) -> None:
        self.connection.close()

    def initialize(self) -> None:
        self.connection.executescript(
            """
            create table if not exists tool_docs (
                id integer primary key,
                tool text not null,
                shell text not null,
                source text not null,
                version text,
                content text not null,
                updated_at text not null default current_timestamp,
                unique(tool, shell, source)
            );

            drop table if exists tool_docs_fts;

            create virtual table tool_docs_fts using fts5(
                doc_id unindexed,
                tool,
                shell,
                source,
                content
            );

            create table if not exists ask_history (
                id integer primary key,
                query text not null,
                shell text not null,
                inferred_tool text,
                command text,
                source text not null,
                note text,
                created_at text not null default current_timestamp
            );

            create table if not exists command_catalog (
                name text not null,
                shell text not null,
                source text not null,
                synopsis text,
                syntax text,
                aliases text,
                updated_at text not null default current_timestamp,
                primary key(name, shell)
            );

            drop table if exists command_catalog_fts;

            create virtual table command_catalog_fts using fts5(
                name,
                shell,
                source,
                synopsis,
                syntax,
                aliases
            );
            """
        )
        self.connection.commit()
        self._rebuild_fts()
        self._rebuild_catalog_fts()

    def upsert_doc(self, capture: HelpCapture) -> int:
        row = self.connection.execute(
            """
            insert into tool_docs(tool, shell, source, version, content, updated_at)
            values (?, ?, ?, ?, ?, current_timestamp)
            on conflict(tool, shell, source) do update set
                version=excluded.version,
                content=excluded.content,
                updated_at=current_timestamp
            returning id
            """,
            (capture.tool, capture.shell, capture.source, capture.version, capture.content),
        ).fetchone()
        self.connection.commit()
        self._rebuild_fts()
        return int(row["id"])

    def replace_docs(self, tool: str, shell: str, captures: Iterable[HelpCapture]) -> int:
        capture_list = list(captures)
        self.connection.execute(
            "delete from tool_docs where lower(tool)=lower(?) and lower(shell)=lower(?)",
            (tool, shell),
        )
        for capture in capture_list:
            self.connection.execute(
                """
                insert into tool_docs(tool, shell, source, version, content, updated_at)
                values (?, ?, ?, ?, ?, current_timestamp)
                """,
                (capture.tool, capture.shell, capture.source, capture.version, capture.content),
            )
        self.connection.commit()
        self._rebuild_fts()
        return len(capture_list)

    def list_tools(self) -> list[tuple[str, str, int, str]]:
        rows = self.connection.execute(
            """
            select tool, shell, count(*) as doc_count, max(updated_at) as latest
            from tool_docs
            group by tool, shell
            order by lower(tool), lower(shell)
            """
        ).fetchall()
        return [(row["tool"], row["shell"], int(row["doc_count"]), row["latest"]) for row in rows]

    def docs_for_tool(self, tool: str, shell: str | None = None, limit: int = 5) -> list[ToolDoc]:
        sql = "select * from tool_docs where lower(tool)=lower(?)"
        params: list[object] = [tool]
        if shell:
            sql += " and lower(shell)=lower(?)"
            params.append(shell)
        sql += " order by updated_at desc limit ?"
        params.append(limit)
        return [self._row_to_doc(row) for row in self.connection.execute(sql, params)]

    def search(self, query: str, tool: str | None = None, shell: str | None = None, limit: int = 6) -> list[SearchHit]:
        fts_query = _to_fts_query(query)
        if not fts_query:
            return []

        params: list[object] = [fts_query]
        filters = ["tool_docs_fts match ?"]
        if tool:
            filters.append("lower(tool_docs.tool)=lower(?)")
            params.append(tool)
        if shell:
            filters.append("lower(tool_docs.shell)=lower(?)")
            params.append(shell)
        params.append(limit)

        rows = self.connection.execute(
            f"""
            select tool_docs.*,
                   bm25(tool_docs_fts) as rank,
                   snippet(tool_docs_fts, 4, '', '', ' ... ', 20) as snippet_text
            from tool_docs_fts
            join tool_docs on tool_docs_fts.doc_id = tool_docs.id
            where {" and ".join(filters)}
            order by rank
            limit ?
            """,
            params,
        ).fetchall()
        return [
            SearchHit(self._row_to_doc(row), float(row["rank"]), _collapse(row["snippet_text"]))
            for row in rows
        ]

    def search_any(
        self,
        query: str,
        tool: str | None = None,
        shell: str | None = None,
        limit: int = 6,
    ) -> list[SearchHit]:
        hits = self.search(query, tool=tool, shell=shell, limit=limit)
        if hits:
            return hits
        tokens = _query_tokens(query)
        for token in tokens:
            hits = self.search(token, tool=tool, shell=shell, limit=limit)
            if hits:
                return hits
        return []

    def record_ask(
        self,
        query: str,
        shell: str,
        inferred_tool: str | None,
        command: str | None,
        source: str,
        note: str | None = None,
    ) -> int:
        row = self.connection.execute(
            """
            insert into ask_history(query, shell, inferred_tool, command, source, note)
            values (?, ?, ?, ?, ?, ?)
            returning id
            """,
            (query, shell, inferred_tool, command, source, note),
        ).fetchone()
        self.connection.commit()
        return int(row["id"])

    def list_ask_history(self, limit: int = 20) -> list[AskHistoryEntry]:
        rows = self.connection.execute(
            """
            select * from ask_history
            order by datetime(created_at) desc, id desc
            limit ?
            """,
            (limit,),
        ).fetchall()
        return [self._row_to_history(row) for row in rows]

    def usage_by_tool(self, limit: int = 20) -> list[tuple[str, int]]:
        rows = self.connection.execute(
            """
            select coalesce(inferred_tool, '(unknown)') as tool, count(*) as count
            from ask_history
            group by coalesce(inferred_tool, '(unknown)')
            order by count desc, lower(tool)
            limit ?
            """,
            (limit,),
        ).fetchall()
        return [(row["tool"], int(row["count"])) for row in rows]

    def usage_by_command(self, limit: int = 20) -> list[tuple[str, int]]:
        rows = self.connection.execute(
            """
            select command, count(*) as count
            from ask_history
            where command is not null and trim(command) <> ''
            group by command
            order by count desc, lower(command)
            limit ?
            """,
            (limit,),
        ).fetchall()
        return [(row["command"], int(row["count"])) for row in rows]

    def usage_by_source(self) -> list[tuple[str, int]]:
        rows = self.connection.execute(
            """
            select source, count(*) as count
            from ask_history
            group by source
            order by count desc, lower(source)
            """
        ).fetchall()
        return [(row["source"], int(row["count"])) for row in rows]

    def upsert_command_catalog(
        self,
        entries: Iterable[CommandCatalogEntry],
    ) -> int:
        entry_list = list(entries)
        for entry in entry_list:
            self.connection.execute(
                """
                insert into command_catalog(name, shell, source, synopsis, syntax, aliases, updated_at)
                values (?, ?, ?, ?, ?, ?, current_timestamp)
                on conflict(name, shell) do update set
                    source=excluded.source,
                    synopsis=excluded.synopsis,
                    syntax=excluded.syntax,
                    aliases=excluded.aliases,
                    updated_at=current_timestamp
                """,
                (entry.name, entry.shell, entry.source, entry.synopsis, entry.syntax, entry.aliases),
            )
        self.connection.commit()
        self._rebuild_catalog_fts()
        return len(entry_list)

    def replace_command_catalog(self, shell: str, entries: Iterable[CommandCatalogEntry]) -> int:
        entry_list = list(entries)
        self.connection.execute(
            "delete from command_catalog where lower(shell)=lower(?)",
            (shell,),
        )
        self.connection.commit()
        return self.upsert_command_catalog(entry_list)

    def delete_catalog_by_source(self, shell: str, source: str) -> None:
        self.connection.execute(
            "delete from command_catalog where lower(shell)=lower(?) and lower(source)=lower(?)",
            (shell, source),
        )
        self.connection.commit()


    def catalog_count(self, shell: str | None = None) -> int:
        if shell:
            row = self.connection.execute(
                "select count(*) as count from command_catalog where lower(shell)=lower(?)",
                (shell,),
            ).fetchone()
        else:
            row = self.connection.execute("select count(*) as count from command_catalog").fetchone()
        return int(row["count"])

    def search_catalog(self, query: str, shell: str, limit: int = 8) -> list[CommandCatalogEntry]:
        fts_query = _to_fts_query(query)
        if not fts_query:
            return []
        rows = self.connection.execute(
            """
            select command_catalog.*
            from command_catalog_fts
            join command_catalog
              on command_catalog_fts.name = command_catalog.name
             and command_catalog_fts.shell = command_catalog.shell
            where command_catalog_fts match ?
              and lower(command_catalog.shell)=lower(?)
            order by bm25(command_catalog_fts)
            limit ?
            """,
            (fts_query, shell, limit),
        ).fetchall()
        return [self._row_to_catalog(row) for row in rows]

    def catalog_names(self, shell: str, limit: int = 500) -> list[str]:
        rows = self.connection.execute(
            """
            select name
            from command_catalog
            where lower(shell)=lower(?)
            order by lower(name)
            limit ?
            """,
            (shell, limit),
        ).fetchall()
        return [row["name"] for row in rows]

    def catalog_entry(self, name: str, shell: str) -> CommandCatalogEntry | None:
        row = self.connection.execute(
            """
            select *
            from command_catalog
            where lower(name)=lower(?) and lower(shell)=lower(?)
            """,
            (name, shell),
        ).fetchone()
        return self._row_to_catalog(row) if row else None

    def _rebuild_fts(self) -> None:
        self.connection.execute("delete from tool_docs_fts")
        self.connection.execute(
            """
            insert into tool_docs_fts(doc_id, tool, shell, source, content)
            select id, tool, shell, source, content from tool_docs
            """
        )
        self.connection.commit()

    def _rebuild_catalog_fts(self) -> None:
        self.connection.execute("delete from command_catalog_fts")
        self.connection.execute(
            """
            insert into command_catalog_fts(name, shell, source, synopsis, syntax, aliases)
            select name, shell, source, coalesce(synopsis, ''), coalesce(syntax, ''), coalesce(aliases, '')
            from command_catalog
            """
        )
        self.connection.commit()

    @staticmethod
    def _row_to_doc(row: sqlite3.Row) -> ToolDoc:
        return ToolDoc(
            id=int(row["id"]),
            tool=row["tool"],
            shell=row["shell"],
            source=row["source"],
            version=row["version"],
            content=row["content"],
            updated_at=row["updated_at"],
        )

    @staticmethod
    def _row_to_history(row: sqlite3.Row) -> AskHistoryEntry:
        return AskHistoryEntry(
            id=int(row["id"]),
            query=row["query"],
            shell=row["shell"],
            inferred_tool=row["inferred_tool"],
            command=row["command"],
            source=row["source"],
            note=row["note"],
            created_at=row["created_at"],
        )

    @staticmethod
    def _row_to_catalog(row: sqlite3.Row) -> CommandCatalogEntry:
        return CommandCatalogEntry(
            name=row["name"],
            shell=row["shell"],
            source=row["source"],
            synopsis=row["synopsis"],
            syntax=row["syntax"],
            aliases=row["aliases"],
            updated_at=row["updated_at"],
        )


def _query_tokens(query: str) -> list[str]:
    tokens: list[str] = []
    for token in re.findall(r"[a-zA-Z0-9_]+", query.lower()):
        if len(token) < 2:
            continue
        tokens.append(token)
        tokens.extend(_token_variants(token))
    return list(dict.fromkeys(tokens))


def _token_variants(token: str) -> Iterable[str]:
    if token.endswith("ly") and len(token) > 5:
        yield token[:-2]
    if token.endswith("ively") and len(token) > 8:
        yield token[:-4] + "e"
    if token.endswith("ing") and len(token) > 6:
        yield token[:-3]
    if token.endswith("es") and len(token) > 5:
        yield token[:-2]
    if token.endswith("s") and len(token) > 4:
        yield token[:-1]


def _to_fts_query(query: str) -> str:
    tokens = _query_tokens(query)
    return " OR ".join(f'"{token}"' for token in tokens[:12])


def _collapse(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()
