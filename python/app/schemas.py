from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field


class JsonDatasetIn(BaseModel):
    id: str | None = None
    name: str | None = "dataset"
    columns: list[Any]
    rows: list[Any]


class TrainIn(BaseModel):
    datasetId: str
    targetColumn: str
    algorithm: str | None = "gbr"
    task: str | None = "auto"


class WhatIfIn(BaseModel):
    rowIndex: int | None = None
    row: dict[str, Any] | None = None
    edits: dict[str, Any] | None = Field(default_factory=dict)
