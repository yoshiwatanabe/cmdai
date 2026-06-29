from __future__ import annotations

import argparse
import os
import re
import sys


def supports_color() -> bool:
    if not sys.stdout.isatty():
        return False
    if "NO_COLOR" in os.environ:
        return False
    if os.name == "nt":
        try:
            import ctypes
            kernel32 = ctypes.windll.kernel32
            h = kernel32.GetStdHandle(-11)
            mode = ctypes.c_ulong()
            if kernel32.GetConsoleMode(h, ctypes.byref(mode)):
                kernel32.SetConsoleMode(h, mode.value | 0x0004)
                return True
        except Exception:
            return "ANSICON" in os.environ or os.environ.get("TERM") in {"xterm", "xterm-256color"} or os.environ.get("COLORTERM") is not None
    return True


_HAS_COLOR = supports_color()


def style(text: str, fg: str | None = None, bold: bool = False, dim: bool = False) -> str:
    if not _HAS_COLOR:
        return text
    codes = []
    if bold:
        codes.append("1")
    if dim:
        codes.append("90")
    if fg:
        fg_codes = {
            "green": "32",
            "cyan": "36",
            "yellow": "33",
            "red": "31",
            "blue": "34",
            "magenta": "35",
            "white": "37",
        }
        if fg in fg_codes:
            codes.append(fg_codes[fg])
    if not codes:
        return text
    return f"\033[{';'.join(codes)}m{text}\033[0m"


def format_colored_ask_output(text: str) -> str:
    colored_lines = []
    for line in text.splitlines():
        if line.startswith("Command:"):
            parts = line.split(":", 1)
            cmd_val = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style('Command:', fg='cyan', bold=True)} {style(cmd_val.strip(), fg='green', bold=True)}")
        elif line.startswith("Why:"):
            parts = line.split(":", 1)
            why_val = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style('Why:', fg='cyan', bold=True)} {style(why_val.strip(), fg='white')}")
        elif line.startswith("Docs:"):
            parts = line.split(":", 1)
            docs_val = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style('Docs:', fg='cyan', bold=True)} {style(docs_val.strip(), dim=True)}")
        elif line.startswith("Grooming:"):
            parts = line.split(":", 1)
            groom_val = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style('Grooming:', fg='yellow', bold=True)} {style(groom_val.strip(), dim=True)}")
        elif line.startswith("Note:"):
            parts = line.split(":", 1)
            note_val = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style('Note:', fg='yellow', bold=True)} {style(note_val.strip(), fg='yellow')}")
        elif line.startswith("No grounded command"):
            colored_lines.append(style(line, fg="red", bold=True))
        elif re.match(r"^\d+\.", line.strip()):
            parts = line.split(".", 1)
            num = parts[0]
            rest = parts[1] if len(parts) > 1 else ""
            colored_lines.append(f"{style(num + '.', fg='cyan')} {style(rest, dim=True)}")
        elif line.startswith("   "):
            colored_lines.append(style(line, dim=True))
        else:
            colored_lines.append(line)
    return "\n".join(colored_lines)

from .catalog import discover_shell_commands
from .collector import capture_tool_help
from .env_file import load_env_files
from .llm import LlmUnavailable, get_gemini_model, test_gemini_connectivity
from .resolver import resolve_query_detail
from .storage import CacheStore, default_db_path


def main(argv: list[str] | None = None) -> int:
    raw_argv = list(sys.argv[1:] if argv is None else argv)
    if not raw_argv:
        raw_argv = ["--help"]
    commands = {"ask", "groom", "groom-shell", "tools", "history", "stats", "doctor", "cache", "catalog"}
    if raw_argv and raw_argv[0] not in commands and raw_argv[0] not in {"-h", "--help"}:
        raw_argv = ["ask", *raw_argv]

    parser = build_parser()
    args = parser.parse_args(raw_argv)
    loaded_env_files = load_env_files()
    store = CacheStore()
    try:
        setattr(args, "loaded_env_files", loaded_env_files)
        return args.handler(args, store)
    finally:
        store.close()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="cmdai", description="Documentation-grounded CLI assistant")
    subparsers = parser.add_subparsers(dest="command", required=True)

    ask = subparsers.add_parser("ask", help="Resolve a natural-language request")
    ask.add_argument("query", nargs="+")
    ask.add_argument("--tool")
    ask.add_argument("--shell", default="powershell", choices=["powershell", "bash", "cmd"])
    ask.add_argument("--no-ai", action="store_true")
    ask.add_argument("--no-auto-groom", action="store_true", help="Suggest grooming instead of capturing help automatically")
    ask.set_defaults(handler=handle_ask)

    groom = subparsers.add_parser("groom", help="Capture local help output for tools")
    groom.add_argument("tools", nargs="+")
    groom.add_argument("--shell", default="powershell", choices=["powershell", "bash", "cmd"])
    groom.set_defaults(handler=handle_groom)

    groom_shell = subparsers.add_parser("groom-shell", help="Discover commands available in a shell")
    groom_shell.add_argument("shell", choices=["powershell"])
    groom_shell.add_argument("--module", help="Limit cmdlet discovery to a specific module")
    groom_shell.set_defaults(handler=handle_groom_shell)

    tools = subparsers.add_parser("tools", help="List cached tools")
    tools.set_defaults(handler=handle_tools)

    history = subparsers.add_parser("history", help="List recent asks")
    history.add_argument("--limit", type=int, default=20)
    history.set_defaults(handler=handle_history)

    stats = subparsers.add_parser("stats", help="Show usage metrics")
    stats.add_argument("--limit", type=int, default=10)
    stats.set_defaults(handler=handle_stats)

    doctor = subparsers.add_parser("doctor", help="Show runtime information")
    doctor.add_argument("--test-llm", action="store_true", help="Make a live Gemini connectivity test")
    doctor.set_defaults(handler=handle_doctor)

    cache = subparsers.add_parser("cache", help="Inspect cached docs")
    cache_subparsers = cache.add_subparsers(dest="cache_command", required=True)
    search = cache_subparsers.add_parser("search", help="Search cached docs")
    search.add_argument("query", nargs="+")
    search.add_argument("--tool")
    search.add_argument("--shell")
    search.set_defaults(handler=handle_cache_search)

    show = cache_subparsers.add_parser("show", help="Show docs for a tool")
    show.add_argument("tool")
    show.add_argument("--shell")
    show.add_argument("--limit", type=int, default=3)
    show.set_defaults(handler=handle_cache_show)

    catalog = subparsers.add_parser("catalog", help="Inspect discovered command catalog")
    catalog_subparsers = catalog.add_subparsers(dest="catalog_command", required=True)
    catalog_search = catalog_subparsers.add_parser("search", help="Search discovered commands")
    catalog_search.add_argument("query", nargs="+")
    catalog_search.add_argument("--shell", default="powershell")
    catalog_search.set_defaults(handler=handle_catalog_search)

    return parser


def handle_ask(args: argparse.Namespace, store: CacheStore) -> int:
    resolution = resolve_query_detail(
        store,
        " ".join(args.query),
        tool=args.tool,
        shell=args.shell,
        use_llm=not args.no_ai,
        auto_groom=not args.no_auto_groom,
    )
    store.record_ask(
        resolution.query,
        resolution.shell,
        resolution.inferred_tool,
        resolution.command,
        resolution.source,
        resolution.note,
    )
    return _print(format_colored_ask_output(resolution.text))


def handle_groom(args: argparse.Namespace, store: CacheStore) -> int:
    saved = 0
    for tool in args.tools:
        captures = capture_tool_help(tool, shell=args.shell)
        if not captures:
            print(f"{tool}: no useful help output captured")
            continue
        saved += store.replace_docs(tool.lower(), args.shell.lower(), captures)
        sources = ", ".join(capture.source for capture in captures)
        print(f"{tool}: cached {len(captures)} doc(s): {sources}")
    return 0 if saved else 1


def handle_groom_shell(args: argparse.Namespace, store: CacheStore) -> int:
    module_log = f" module '{args.module}'" if args.module else ""
    entries = discover_shell_commands(args.shell, module=args.module)
    if not entries:
        print(f"{args.shell}: no commands discovered{module_log}")
        return 1
    if args.module:
        store.delete_catalog_by_source(args.shell, args.module)
        count = store.upsert_command_catalog(entries)
    else:
        count = store.replace_command_catalog(args.shell, entries)
    print(f"{args.shell}: discovered {count} command(s){module_log}")
    return 0


def handle_tools(args: argparse.Namespace, store: CacheStore) -> int:
    rows = store.list_tools()
    if not rows:
        print(style("No cached tools found. Run: cmdai-next groom git grep", fg="yellow"))
        return 0
    
    print(style(f"{'TOOL':<20} {'SHELL':<12} {'DOCS':<10} {'LATEST UPDATE':<20}", bold=True, fg="cyan"))
    print(style("-" * 65, dim=True))
    for tool, shell, count, latest in rows:
        print(f"{style(tool, fg='white'):<20} {style(shell, fg='magenta'):<12} {style(f'{count} doc(s)', fg='green'):<10} {style(latest, dim=True):<20}")
    return 0


def handle_history(args: argparse.Namespace, store: CacheStore) -> int:
    entries = store.list_ask_history(args.limit)
    if not entries:
        print(style("No ask history yet.", fg="yellow"))
        return 0
    
    print(style("=== Ask History ===", bold=True, fg="cyan"))
    print()
    for entry in entries:
        header = f"[{style(entry.created_at, dim=True)}] {style(f'#{entry.id}', fg='cyan', bold=True)}"
        if entry.inferred_tool:
            header += f" {style(entry.inferred_tool, fg='magenta')}"
        header += f" ({style(entry.source, dim=True)})"
        print(header)
        print(f"  {style('Ask:', fg='blue')} {entry.query}")
        if entry.command:
            print(f"  {style('Cmd:', fg='green')} {style(entry.command, fg='green', bold=True)}")
        if entry.note:
            print(f"  {style('Note:', fg='yellow')} {style(entry.note, dim=True)}")
        print(style("-" * 65, dim=True))
    return 0


def handle_stats(args: argparse.Namespace, store: CacheStore) -> int:
    print(style("=== Usage Stats ===", bold=True, fg="cyan"))
    print()
    
    tools = store.usage_by_tool(args.limit)
    print(style("Usage by Tool:", bold=True))
    if not tools:
        print("  (no data)")
    else:
        max_tool_count = max(count for _, count in tools) if tools else 1
        for tool, count in tools:
            bar_len = int((count / max_tool_count) * 20)
            bar = style("█" * bar_len, fg="cyan")
            print(f"  {tool:<20} {count:>3} {bar}")

    print()
    sources = store.usage_by_source()
    print(style("Usage by Source:", bold=True))
    if not sources:
        print("  (no data)")
    else:
        max_source_count = max(count for _, count in sources) if sources else 1
        for source, count in sources:
            bar_len = int((count / max_source_count) * 20)
            bar = style("█" * bar_len, fg="magenta")
            print(f"  {source:<20} {count:>3} {bar}")

    print()
    print(style("Frequent Commands:", bold=True))
    commands = store.usage_by_command(args.limit)
    if not commands:
        print("  none")
    for command, count in commands:
        print(f"  {count:>3}x  {style(command, fg='green', bold=True)}")
    return 0


def handle_doctor(args: argparse.Namespace, store: CacheStore) -> int:
    print(style("=== Runtime Diagnostics ===", bold=True, fg="cyan"))
    print(f"Database:                     {style(str(default_db_path()), fg='white')}")
    print(f"Cached tool groups:           {style(str(len(store.list_tools())), fg='white')}")
    print(f"PowerShell catalog entries:   {style(str(store.catalog_count('powershell')), fg='white')}")
    
    loaded_env_files = getattr(args, "loaded_env_files", [])
    if loaded_env_files:
        print("Loaded env files:")
        for path in loaded_env_files:
            print(f"  {style(str(path), dim=True)}")
    else:
        print("Loaded env files:             none")
        
    has_key = bool(os.environ.get("CMDAI_GEMINI_API_KEY") or os.environ.get("GEMINI_API_KEY"))
    key_status = style("Yes", fg="green", bold=True) if has_key else style("No", fg="red", bold=True)
    print(f"Gemini API key configured:    {key_status}")
    print(f"Gemini model:                 {style(get_gemini_model(), fg='white')}")
    
    if args.test_llm:
        try:
            response = test_gemini_connectivity()
            print(f"Gemini connectivity:          {style('OK', fg='green', bold=True)} ({style(response[:80], dim=True)})")
        except LlmUnavailable as exc:
            print(f"Gemini connectivity:          {style('FAILED', fg='red', bold=True)} ({exc})")
            return 1
            
    print()
    print(style("Tips:", dim=True))
    print(style("  Set CMDAI_DB environment variable to move the SQLite cache.", dim=True))
    print(style("  Set GEMINI_API_KEY or CMDAI_GEMINI_API_KEY to enable grounded generation.", dim=True))
    return 0


def handle_cache_search(args: argparse.Namespace, store: CacheStore) -> int:
    hits = store.search_any(" ".join(args.query), tool=args.tool, shell=args.shell, limit=10)
    for hit in hits:
        print(f"{hit.doc.tool}\t{hit.doc.shell}\t{hit.doc.source}\t{hit.snippet}")
    return 0


def handle_cache_show(args: argparse.Namespace, store: CacheStore) -> int:
    docs = store.docs_for_tool(args.tool, shell=args.shell, limit=args.limit)
    if not docs:
        print(f"No docs found for {args.tool}")
        return 1
    for doc in docs:
        print(f"=== {doc.tool} [{doc.shell}, {doc.source}, {doc.updated_at}] ===")
        print(doc.content)
    return 0


def handle_catalog_search(args: argparse.Namespace, store: CacheStore) -> int:
    entries = store.search_catalog(" ".join(args.query), args.shell, limit=10)
    if not entries:
        print("No catalog matches.")
        return 1
    for entry in entries:
        print(f"{entry.name}\t{entry.shell}\t{entry.source}")
        if entry.synopsis:
            print(f"  synopsis: {entry.synopsis}")
        if entry.syntax:
            first_line = entry.syntax.splitlines()[0] if entry.syntax.splitlines() else entry.syntax
            print(f"  syntax: {first_line}")
    return 0


def _print(value: str) -> int:
    print(value)
    return 0
