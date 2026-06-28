from __future__ import annotations

import json
import re
import shutil
import subprocess

from .models import CommandCatalogEntry


def discover_shell_commands(shell: str, module: str | None = None) -> list[CommandCatalogEntry]:
    if shell.lower() not in {"powershell", "pwsh", "ps"}:
        return []
    return discover_powershell_commands(module)


def discover_powershell_commands(module: str | None = None) -> list[CommandCatalogEntry]:
    host = shutil.which("pwsh") or shutil.which("powershell")
    if not host:
        return []

    if module:
        module_setup = f"Import-Module -Name {module} -ErrorAction SilentlyContinue\n$commands = Get-Command -Module {module}"
    else:
        module_setup = "$commands = Get-Command"

    script = f"""
{module_setup} -CommandType Cmdlet,Function,ExternalScript |
    Where-Object {{ $_.Name -and $_.CommandType -ne 'Application' }} |
    Sort-Object Name -Unique

$commands | ForEach-Object {{
    $parameterNames = ''
    try {{
        if ($_.Parameters) {{
            $parameterNames = ($_.Parameters.Keys -join ' ')
        }}
    }} catch {{}}

    [pscustomobject]@{{
        Name = $_.Name
        Shell = 'powershell'
        Source = $_.Source
        Synopsis = (($_.Definition, $parameterNames) -join "`n")
        Syntax = ''
        Aliases = ''
    }}
}} | ConvertTo-Json -Depth 4
"""
    completed = subprocess.run(
        [host, "-NoProfile", "-Command", script],
        capture_output=True,
        text=True,
        timeout=30,
        errors="replace",
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        return []

    data = json.loads(completed.stdout)
    if isinstance(data, dict):
        data = [data]

    entries: list[CommandCatalogEntry] = []
    for item in data:
        name = str(item.get("Name") or "").strip()
        if not name:
            continue
        raw_synopsis = _clean(item.get("Synopsis"))
        entries.append(
            CommandCatalogEntry(
                name=name.lower(),
                shell="powershell",
                source=str(item.get("Source") or "powershell"),
                synopsis=_enrich_search_text(name, raw_synopsis),
                syntax=_clean(item.get("Syntax")),
                aliases=_clean(item.get("Aliases")),
                updated_at="",
            )
        )
    return entries



def _clean(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _enrich_search_text(name: str, synopsis: str | None) -> str:
    parts = [synopsis or "", name.replace("-", " ")]
    parts.extend(_split_words(name))
    if synopsis:
        parts.extend(_split_words(synopsis))
    return " ".join(part for part in parts if part).strip()


def _split_words(value: str) -> list[str]:
    spaced = re.sub(r"([a-z])([A-Z])", r"\1 \2", value)
    return re.findall(r"[A-Za-z][A-Za-z0-9]+", spaced)
