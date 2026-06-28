from __future__ import annotations

from cmdai_next.llm import build_gemini_payload, get_gemini_model_candidates, normalize_gemini_model
from cmdai_next.llm import _extract_text


def test_gemini_payload_uses_generate_content_shape() -> None:
    payload = build_gemini_payload("hello")

    assert payload["contents"][0]["parts"][0]["text"] == "hello"
    assert payload["generationConfig"]["maxOutputTokens"] == 500
    assert "temperature" not in payload


def test_normalize_gemini_model_handles_models_prefix_and_old_default() -> None:
    assert normalize_gemini_model("models/gemini-2.5-flash") == "gemini-2.5-flash"
    assert normalize_gemini_model("gemini-3.5-flash") == "gemini-2.5-flash"


def test_extract_text_from_generate_content_response() -> None:
    body = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {"text": "ok"},
                    ],
                },
            },
        ],
    }

    assert _extract_text(body) == "ok"


def test_gemini_model_candidates_include_fallback(monkeypatch) -> None:
    monkeypatch.setenv("CMDAI_GEMINI_MODEL", "gemini-2.5-flash")
    monkeypatch.setenv("CMDAI_GEMINI_FALLBACK_MODEL", "gemini-flash-lite-latest")

    assert get_gemini_model_candidates() == ["gemini-2.5-flash", "gemini-flash-lite-latest"]
