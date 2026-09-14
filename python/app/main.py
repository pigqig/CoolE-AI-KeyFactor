from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Any

from fastapi import FastAPI, File, Query, Request, UploadFile
from fastapi.responses import JSONResponse

from app.csv_loader import (
    load_csv_bytes,
    load_csv_path,
    load_json_table,
    sample_csv_path,
    sample_okng_csv_path,
    write_sample_csv,
    write_sample_okng_csv,
)
from app.errors import ServiceError
from app.explainer import compute_dependence, compute_importances, compute_rows, compute_whatif
from app.insights import dataset_overview
from app.schemas import JsonDatasetIn, TrainIn, WhatIfIn
from app.artifact import dump_fit, load_fit
from app.store import STORE
from app.trainer import fit_model

app = FastAPI(title="Key Factor sklearn service", version="1.0.0")


@app.exception_handler(ServiceError)
async def service_error_handler(_: Request, exc: ServiceError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status_code,
        content={"errorCode": exc.error_code, "message": exc.message},
    )


@app.exception_handler(Exception)
async def unhandled_error_handler(_: Request, exc: Exception) -> JSONResponse:
    return JSONResponse(
        status_code=500,
        content={
            "errorCode": "pythonError",
            "message": f"Analysis service failed: {type(exc).__name__}: {exc}",
        },
    )


def _column_dicts(columns) -> list[dict[str, Any]]:
    return [c.to_dict() if hasattr(c, "to_dict") else c for c in columns]


def _preview(df, limit: int = 12) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for _, row in df.head(limit).iterrows():
        item = {}
        for key, value in row.items():
            if value is None or (isinstance(value, float) and value != value):
                item[str(key)] = None
            elif hasattr(value, "item"):
                item[str(key)] = value.item()
            else:
                item[str(key)] = None if str(value) == "<NA>" else value
        records.append(item)
    return records


def _dataset_payload(dataset_id: str, name: str, df, columns, target: str | None = None) -> dict[str, Any]:
    overview = dataset_overview(df, columns, target)
    return {
        "id": dataset_id,
        "name": name,
        "rowCount": int(len(df)),
        "columns": _column_dicts(columns),
        "previewRows": _preview(df),
        "overview": overview,
    }


def _save_dataset(name: str, df, columns, dataset_id: str | None = None) -> dict[str, Any]:
    dataset_id = dataset_id or STORE.new_id()
    STORE.put_dataset(
        {
            "id": dataset_id,
            "name": name,
            "df": df,
            "columns": columns,
        }
    )
    return _dataset_payload(dataset_id, name, df, columns)


def _code_stamp() -> str:
    root = Path(__file__).resolve().parent
    digest = hashlib.sha256()
    for path in sorted(root.glob("*.py")):
        digest.update(path.name.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
    return digest.hexdigest()[:16]


# Snapshot at import so a stale worker cannot echo the newer files on disk.
CODE_STAMP = _code_stamp()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "codeStamp": CODE_STAMP}


@app.post("/datasets")
async def upload_dataset(
    file: UploadFile = File(...),
    name: str | None = None,
    id: str | None = None,
) -> dict[str, Any]:
    raw = await file.read()
    df, columns = load_csv_bytes(raw)
    return _save_dataset(name or file.filename or "upload.csv", df, columns, id)


@app.post("/datasets/json")
def json_dataset(body: JsonDatasetIn) -> dict[str, Any]:
    df, columns = load_json_table(body.columns, body.rows)
    return _save_dataset(body.name or "dataset", df, columns, body.id)


def _load_named_sample(kind: str | None) -> dict[str, Any]:
    if (kind or "quality").strip().lower() == "okng":
        path = sample_okng_csv_path()
        if not path.is_file():
            write_sample_okng_csv(path)
        df, columns = load_csv_path(path)
        return _save_dataset("process-okng.csv", df, columns)
    path = sample_csv_path()
    if not path.is_file():
        write_sample_csv(path)
    df, columns = load_csv_path(path)
    return _save_dataset("process-quality.csv", df, columns)


@app.post("/datasets/sample")
def sample_dataset(kind: str | None = Query(default=None)) -> dict[str, Any]:
    return _load_named_sample(kind)


@app.post("/datasets/sample-okng")
def sample_okng_dataset() -> dict[str, Any]:
    return _load_named_sample("okng")


@app.get("/datasets/{dataset_id}")
def get_dataset(dataset_id: str, target: str | None = None) -> dict[str, Any]:
    item = STORE.get_dataset(dataset_id)
    return _dataset_payload(item["id"], item["name"], item["df"], item["columns"], target)


@app.post("/models/train")
def train(body: TrainIn) -> dict[str, Any]:
    dataset = STORE.get_dataset(body.datasetId)
    fitted = fit_model(
        dataset["df"],
        dataset["columns"],
        body.targetColumn,
        body.algorithm or "gbr",
        body.task or "auto",
    )
    importances = compute_importances(fitted)
    fitted["importance_cache"] = importances
    model_id = STORE.new_id()
    fitted["id"] = model_id
    fitted["datasetId"] = body.datasetId
    STORE.put_model(fitted)
    payload = _model_payload(fitted)
    payload["artifactBase64"] = dump_fit(fitted)
    return payload


@app.post("/models/restore")
def restore(body: dict[str, Any]) -> dict[str, Any]:
    blob = body.get("artifactBase64") or ""
    fitted = load_fit(str(blob))
    fitted["id"] = body.get("id") or fitted.get("id") or STORE.new_id()
    STORE.put_model(fitted)
    return _model_payload(fitted)


def _model_payload(model: dict[str, Any]) -> dict[str, Any]:
    metrics = dict(model["metrics"])
    return {
        "id": model["id"],
        "datasetId": model.get("datasetId"),
        "targetColumn": model["target"],
        "task": model["task"],
        "taskKind": model.get("taskKind") or metrics.get("taskKind"),
        "algorithm": model["algorithm"],
        "featureNames": list(model["X"].columns),
        "classes": [str(c) for c in (model.get("classes") or [])],
        "metrics": metrics,
        "overview": dataset_overview(
            STORE.get_dataset(model["datasetId"])["df"] if model.get("datasetId") in STORE.datasets else model["X"],
            [{"name": c, "type": "numeric" if c in model["numeric"] else "categorical"} for c in model["X"].columns]
            + [{"name": model["target"], "type": "numeric" if model["task"] == "regression" else "categorical"}],
            model["target"],
        ),
    }


@app.get("/models/{model_id}")
def get_model(model_id: str) -> dict[str, Any]:
    return _model_payload(STORE.get_model(model_id))


@app.get("/models/{model_id}/importances")
def importances(model_id: str, topN: int | None = Query(default=None)) -> dict[str, Any]:
    model = STORE.get_model(model_id)
    cached = model.get("importance_cache")
    if cached and not topN:
        return cached
    result = compute_importances(model, top_n=topN)
    if not topN:
        model["importance_cache"] = result
    return result


@app.get("/models/{model_id}/dependence")
def dependence(
    model_id: str,
    feature: str = Query(...),
    colorBy: str | None = Query(default=None),
    class_name: str | None = Query(default=None, alias="class"),
) -> dict[str, Any]:
    return compute_dependence(STORE.get_model(model_id), feature, colorBy, class_name)


@app.post("/models/{model_id}/whatif")
def whatif(model_id: str, body: WhatIfIn) -> dict[str, Any]:
    return compute_whatif(STORE.get_model(model_id), body.model_dump())


@app.get("/models/{model_id}/rows")
def rows(model_id: str) -> dict[str, Any]:
    return compute_rows(STORE.get_model(model_id))
