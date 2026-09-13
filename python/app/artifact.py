from __future__ import annotations

import base64
import io
from typing import Any

import joblib

from app.errors import ServiceError


def dump_fit(fitted: dict[str, Any]) -> str:
    buf = io.BytesIO()
    slim = {k: v for k, v in fitted.items() if k != "y_raw"}
    joblib.dump(slim, buf)
    return base64.b64encode(buf.getvalue()).decode("ascii")


def load_fit(b64: str) -> dict[str, Any]:
    try:
        raw = base64.b64decode(b64)
        obj = joblib.load(io.BytesIO(raw))
    except Exception as exc:  # noqa: BLE001
        raise ServiceError("badArtifact", f"Could not restore the fitted model: {exc}", 400) from exc
    if not isinstance(obj, dict) or "pipeline" not in obj:
        raise ServiceError("badArtifact", "Artifact is not a fitted key-factor model.", 400)
    return obj
