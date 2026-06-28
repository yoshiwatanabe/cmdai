from __future__ import annotations

import shutil
import shlex
import subprocess
import os
from dataclasses import dataclass

from .models import HelpCapture
from .supplements import supplemental_docs


DEFAULT_TIMEOUT_SECONDS = 8


@dataclass(frozen=True)
class CaptureAttempt:
    source: str
    command: list[str]


def capture_tool_help(tool: str, shell: str = "powershell", timeout: int = DEFAULT_TIMEOUT_SECONDS) -> list[HelpCapture]:
    captures: list[HelpCapture] = []
    version = _capture_version(tool, timeout)
    for attempt in _attempts(tool, shell):
        content = _run(attempt.command, timeout)
        if _looks_useful(content):
            captures.append(
                HelpCapture(
                    tool=tool.lower(),
                    shell=shell.lower(),
                    source=attempt.source,
                    version=version,
                    content=content,
                )
            )
    captures.extend(supplemental_docs(tool, shell, version))
    return _dedupe(captures)


def _attempts(tool: str, shell: str) -> list[CaptureAttempt]:
    prefix = _command_prefix(tool)
    attempts = [
        CaptureAttempt("--help", [*prefix, "--help"]),
        CaptureAttempt("-h", [*prefix, "-h"]),
    ]
    if len(prefix) == 1:
        attempts.append(CaptureAttempt("help", [*prefix, "help"]))
    if shell.lower() in {"powershell", "pwsh", "ps"} and _is_powershell_topic(tool) and len(prefix) == 1:
        host = shutil.which("pwsh") or shutil.which("powershell")
        if host:
            syntax_command = f"Get-Command {tool} -Syntax | Out-String -Width 240"
            ps_command = f"Get-Help {tool} -Full | Out-String -Width 240"
            attempts.insert(0, CaptureAttempt("Get-Command -Syntax", [host, "-NoProfile", "-Command", syntax_command]))
            attempts.insert(0, CaptureAttempt("Get-Help -Full", [host, "-NoProfile", "-Command", ps_command]))
    return attempts


def _capture_version(tool: str, timeout: int) -> str | None:
    executable = _command_prefix(tool)[0]
    for command in ([executable, "--version"], [executable, "version"], [executable, "-v"]):
        output = _run(list(command), timeout)
        if _looks_useful(output):
            return output.splitlines()[0][:200]
    return None


def _run(command: list[str], timeout: int) -> str:
    try:
        shell = os.name == "nt" and _needs_windows_shell(command[0])
        run_command: list[str] | str = subprocess.list2cmdline(command) if shell else command
        completed = subprocess.run(
            run_command,
            capture_output=True,
            text=True,
            timeout=timeout,
            errors="replace",
            shell=shell,
        )
    except (FileNotFoundError, PermissionError, subprocess.TimeoutExpired):
        return ""
    output = "\n".join(part for part in (completed.stdout, completed.stderr) if part)
    return output.strip()


def _needs_windows_shell(executable: str) -> bool:
    resolved = shutil.which(executable)
    return bool(resolved and resolved.lower().endswith((".cmd", ".bat")))


def _looks_useful(content: str) -> bool:
    if len(content.strip()) < 40:
        return False
    lowered = content.lower()
    bad_markers = ["not recognized", "command not found", "no manual entry"]
    return not any(marker in lowered for marker in bad_markers)


def _dedupe(captures: list[HelpCapture]) -> list[HelpCapture]:
    seen: set[str] = set()
    result: list[HelpCapture] = []
    for capture in captures:
        fingerprint = capture.content[:1000]
        if fingerprint in seen:
            continue
        seen.add(fingerprint)
        result.append(capture)
    return result


def _is_powershell_topic(tool: str) -> bool:
    return "-" in tool or tool.lower().startswith("about_")


def _command_prefix(tool: str) -> list[str]:
    parts = shlex.split(tool, posix=False)
    return parts or [tool]
