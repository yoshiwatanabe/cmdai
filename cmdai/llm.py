from __future__ import annotations

import json
import os
import time
import urllib.error
import urllib.request


class LlmUnavailable(RuntimeError):
    pass


def generate_with_gemini(prompt: str) -> str:
    api_key = os.environ.get("CMDAI_GEMINI_API_KEY") or os.environ.get("GEMINI_API_KEY")
    if not api_key:
        raise LlmUnavailable("Set GEMINI_API_KEY or CMDAI_GEMINI_API_KEY to enable command generation.")

    payload = build_gemini_payload(prompt)
    last_error: LlmUnavailable | None = None
    for model in get_gemini_model_candidates():
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        for attempt in range(2):
            request = urllib.request.Request(
                url,
                data=json.dumps(payload).encode("utf-8"),
                headers={
                    "x-goog-api-key": api_key,
                    "Content-Type": "application/json",
                },
                method="POST",
            )
            try:
                with urllib.request.urlopen(request, timeout=30) as response:
                    body = json.loads(response.read().decode("utf-8"))
                text = _extract_text(body).strip()
                if text:
                    return text
                raise LlmUnavailable(f"Gemini model {model} returned no text.")
            except urllib.error.HTTPError as exc:
                detail = exc.read().decode("utf-8", errors="replace")
                last_error = LlmUnavailable(f"Gemini model {model} failed: HTTP {exc.code}: {detail[:500]}")
                if exc.code not in {429, 500, 502, 503, 504}:
                    raise last_error from exc
                if attempt == 1:
                    break
            except OSError as exc:
                last_error = LlmUnavailable(f"Gemini model {model} failed: {exc}")
                if attempt == 1:
                    break
            time.sleep(1)
    raise last_error or LlmUnavailable("Gemini request failed.")


def test_gemini_connectivity() -> str:
    return generate_with_gemini("Reply with exactly: ok")


def choose_command_candidates(query: str, command_names: list[str], limit: int = 5) -> list[str]:
    if not command_names:
        return []
    prompt = (
        "Choose likely PowerShell command names for the user request.\n"
        "Use only names from the provided list. Return a JSON array of command names, no markdown.\n"
        f"User request: {query}\n"
        f"Command names: {', '.join(command_names[:250])}\n"
    )
    try:
        text = generate_with_gemini(prompt)
    except LlmUnavailable:
        return []
    try:
        data = json.loads(_strip_code_fence(text))
    except json.JSONDecodeError:
        return []
    if not isinstance(data, list):
        return []
    allowed = {name.lower(): name for name in command_names}
    result: list[str] = []
    for item in data:
        name = str(item).strip().lower()
        if name in allowed and allowed[name] not in result:
            result.append(allowed[name])
        if len(result) >= limit:
            break
    return result


def choose_catalog_candidates(
    query: str,
    candidates: list[tuple[str, str]],
    limit: int = 5,
) -> list[str]:
    if not candidates:
        return []
    candidate_lines = "\n".join(
        f"- {name}: {summary[:500].replace(chr(10), ' ')}"
        for name, summary in candidates[:40]
    )
    prompt = (
        "Choose likely PowerShell commands for the user request.\n"
        "Use only command names from the provided candidates. Return a JSON array of command names, no markdown.\n"
        "Choose commands whose documented purpose performs the requested action. "
        "Do not choose object projection/formatting commands unless the request is specifically about selecting object properties or formatting output.\n"
        f"User request: {query}\n"
        f"Candidates:\n{candidate_lines}\n"
    )
    try:
        text = generate_with_gemini(prompt)
    except LlmUnavailable:
        return []
    try:
        data = json.loads(_strip_code_fence(text))
    except json.JSONDecodeError:
        return []
    if not isinstance(data, list):
        return []
    allowed = {name.lower(): name for name, _ in candidates}
    result: list[str] = []
    for item in data:
        name = str(item).strip().lower()
        if name in allowed and allowed[name] not in result:
            result.append(allowed[name])
        if len(result) >= limit:
            break
    return result


def suggest_catalog_search_terms(query: str, limit: int = 8) -> list[str]:
    prompt = (
        "Generate short search terms for finding relevant PowerShell commands in a local command catalog.\n"
        "Return a JSON array of lowercase terms or phrases, no markdown. Include synonyms when useful.\n"
        f"User request: {query}\n"
    )
    try:
        text = generate_with_gemini(prompt)
    except LlmUnavailable:
        return []
    try:
        data = json.loads(_strip_code_fence(text))
    except json.JSONDecodeError:
        return []
    if not isinstance(data, list):
        return []
    terms: list[str] = []
    for item in data:
        term = str(item).strip().lower()
        if term and term not in terms:
            terms.append(term)
        if len(terms) >= limit:
            break
    return terms


def build_gemini_payload(prompt: str) -> dict:
    return {
        "contents": [
            {
                "parts": [
                    {"text": prompt},
                ],
            }
        ],
        "generationConfig": {
            "maxOutputTokens": 500,
        },
    }


def _extract_text(body: dict) -> str:
    parts: list[str] = []
    for candidate in body.get("candidates", []):
        content = candidate.get("content", {})
        for part in content.get("parts", []):
            text = part.get("text")
            if isinstance(text, str):
                parts.append(text)
    return "\n".join(parts)


def normalize_gemini_model(model: str) -> str:
    normalized = model.strip()
    if normalized.startswith("models/"):
        normalized = normalized.removeprefix("models/")
    if normalized == "gemini-3.5-flash":
        return "gemini-2.5-flash"
    return normalized


def get_gemini_model() -> str:
    return normalize_gemini_model(os.environ.get("CMDAI_GEMINI_MODEL", "gemini-2.5-flash"))


def get_gemini_model_candidates() -> list[str]:
    primary = get_gemini_model()
    fallback = normalize_gemini_model(os.environ.get("CMDAI_GEMINI_FALLBACK_MODEL", "gemini-flash-lite-latest"))
    return list(dict.fromkeys([primary, fallback]))


def _strip_code_fence(text: str) -> str:
    stripped = text.strip()
    if not stripped.startswith("```"):
        return stripped
    lines = stripped.splitlines()
    if lines and lines[0].startswith("```"):
        lines = lines[1:]
    if lines and lines[-1].startswith("```"):
        lines = lines[:-1]
    return "\n".join(lines).strip()
