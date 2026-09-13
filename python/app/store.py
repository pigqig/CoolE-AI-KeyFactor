from __future__ import annotations

import threading
import uuid
from typing import Any

from app.errors import ServiceError


class MemoryStore:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self.datasets: dict[str, dict[str, Any]] = {}
        self.models: dict[str, dict[str, Any]] = {}

    def new_id(self) -> str:
        return uuid.uuid4().hex

    def put_dataset(self, payload: dict[str, Any]) -> str:
        with self._lock:
            dataset_id = payload.get("id") or self.new_id()
            payload["id"] = dataset_id
            self.datasets[dataset_id] = payload
            return dataset_id

    def get_dataset(self, dataset_id: str) -> dict[str, Any]:
        with self._lock:
            data = self.datasets.get(dataset_id)
        if not data:
            raise ServiceError("datasetNotFound", f"Dataset '{dataset_id}' was not found.", 404)
        return data

    def put_model(self, payload: dict[str, Any]) -> str:
        with self._lock:
            model_id = payload.get("id") or self.new_id()
            payload["id"] = model_id
            self.models[model_id] = payload
            return model_id

    def get_model(self, model_id: str) -> dict[str, Any]:
        with self._lock:
            data = self.models.get(model_id)
        if not data:
            raise ServiceError("modelNotFound", f"Model '{model_id}' was not found.", 404)
        return data


STORE = MemoryStore()
