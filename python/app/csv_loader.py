from __future__ import annotations

from io import BytesIO, StringIO
from pathlib import Path
from typing import Any, Iterable

import pandas as pd

from app.errors import ServiceError

NUMERIC_RATIO = 0.8


class ColumnInfo:
    def __init__(self, name: str, type_: str, missing_count: int = 0):
        self.name = name
        self.type = type_
        self.missing_count = missing_count

    def to_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "type": self.type,
            "missingCount": self.missing_count,
        }


def _is_numeric(series: pd.Series) -> bool:
    if pd.api.types.is_numeric_dtype(series):
        return True
    coerced = pd.to_numeric(series, errors="coerce")
    non_null = int(series.notna().sum())
    if non_null == 0:
        return False
    return float(coerced.notna().sum()) / non_null >= NUMERIC_RATIO


def _drop_empty_columns(df: pd.DataFrame) -> pd.DataFrame:
    drop: list[Any] = []
    for col in df.columns:
        name = str(col).strip()
        unnamed = name == "" or name.startswith("Unnamed:")
        all_missing = bool(df[col].isna().all())
        if all_missing and unnamed:
            drop.append(col)
    if drop:
        return df.drop(columns=drop)
    return df


def infer_columns(df: pd.DataFrame) -> list[ColumnInfo]:
    columns: list[ColumnInfo] = []
    for col in df.columns:
        series = df[col]
        if _is_numeric(series):
            df[col] = pd.to_numeric(series, errors="coerce")
            typ = "numeric"
        else:
            cleaned = series.astype("string")
            df[col] = cleaned.astype(object).where(cleaned.notna(), None)
            typ = "categorical"
        columns.append(ColumnInfo(str(col), typ, int(pd.isna(df[col]).sum())))
    return columns


def load_frame(df: pd.DataFrame) -> tuple[pd.DataFrame, list[ColumnInfo]]:
    if df is None or df.empty:
        raise ServiceError("emptyDataset", "Dataset is empty.", 400)
    df = df.copy()
    df.columns = [str(c).strip() for c in df.columns]
    df = _drop_empty_columns(df)
    if df.shape[1] == 0:
        raise ServiceError("emptyDataset", "Dataset is empty.", 400)
    if any(c == "" for c in df.columns):
        raise ServiceError("invalidColumns", "Column names must not be empty.", 400)
    if df.columns.duplicated().any():
        raise ServiceError("duplicateColumns", "Duplicate column names are not allowed.", 400)
    columns = infer_columns(df)
    return df, columns


def load_csv_bytes(raw: bytes) -> tuple[pd.DataFrame, list[ColumnInfo]]:
    if not raw or not raw.strip():
        raise ServiceError("emptyDataset", "CSV file is empty.", 400)
    try:
        df = pd.read_csv(BytesIO(raw))
    except Exception as exc:  # noqa: BLE001
        raise ServiceError("invalidCsv", f"Unable to parse CSV: {exc}", 400) from exc
    return load_frame(df)


def load_csv_path(path: str | Path) -> tuple[pd.DataFrame, list[ColumnInfo]]:
    path = Path(path)
    if not path.is_file():
        raise ServiceError("sampleMissing", f"Sample file not found: {path}", 404)
    return load_csv_bytes(path.read_bytes())


def load_json_table(columns: Iterable[Any], rows: Iterable[Any]) -> tuple[pd.DataFrame, list[ColumnInfo]]:
    col_names: list[str] = []
    for col in columns:
        if isinstance(col, dict):
            name = str(col.get("name") or "").strip()
        else:
            name = str(col).strip()
        if not name:
            raise ServiceError("invalidColumns", "Column names must not be empty.", 400)
        col_names.append(name)

    if not col_names:
        raise ServiceError("invalidColumns", "At least one column is required.", 400)

    records: list[dict[str, Any]] = []
    for row in rows:
        if isinstance(row, dict):
            records.append({name: row.get(name) for name in col_names})
        elif isinstance(row, (list, tuple)):
            padded = list(row) + [None] * max(0, len(col_names) - len(row))
            records.append({name: padded[i] for i, name in enumerate(col_names)})
        else:
            raise ServiceError("invalidRows", "Each row must be an object or an array.", 400)

    if not records:
        raise ServiceError("emptyDataset", "Dataset is empty.", 400)

    df = pd.DataFrame.from_records(records, columns=col_names)
    return load_frame(df)


def validate_target(df: pd.DataFrame, target: str) -> None:
    if not target:
        raise ServiceError("missingTarget", "Target column is required.", 400)
    if target not in df.columns:
        raise ServiceError("missingTarget", f"Target column '{target}' was not found.", 400)
    if df[target].dropna().empty:
        raise ServiceError("emptyTarget", f"Target column '{target}' has no usable values.", 400)


def sample_csv_path() -> Path:
    env = Path(__import__("os").environ.get("KEYFACTOR_SAMPLE_CSV", ""))
    if env and env.is_file():
        return env
    here = Path(__file__).resolve()
    candidates = [
        here.parents[2] / "samples" / "process-quality.csv",
        here.parents[1] / "samples" / "process-quality.csv",
        Path.cwd() / "samples" / "process-quality.csv",
    ]
    for path in candidates:
        if path.is_file():
            return path
    return candidates[0]


def generate_process_quality(n: int = 200, seed: int = 42) -> pd.DataFrame:
    import numpy as np

    # Shop-floor reman sheet: conductivity is driven hard by rework heat.
    rng = np.random.default_rng(seed)
    temperature = rng.normal(182, 14, n).clip(148, 218)
    pressure = rng.normal(2.2, 0.35, n).clip(1.2, 3.4)
    time_sec = rng.normal(45, 10, n).clip(15, 90)
    additive = rng.normal(2.6, 0.7, n).clip(0.5, 4.8)
    humidity = rng.normal(48, 9, n).clip(22, 78)
    preheat = rng.normal(18, 5, n).clip(6, 36)
    cool_rate = rng.normal(2.1, 0.4, n).clip(0.8, 3.6)
    line = rng.choice(["L1", "L2", "L3"], size=n, p=[0.42, 0.35, 0.23])
    line_offset = np.where(line == "L1", 0.28, np.where(line == "L2", 0.0, -0.22))

    conductivity = (
        12.0
        + 0.16 * (temperature - 182)
        + 1.05 * (additive - 2.6)
        + 0.012 * (temperature - 182) * (additive - 2.6)
        + 0.07 * (time_sec - 45)
        + 0.55 * (pressure - 2.2)
        + 0.08 * (preheat - 18)
        - 0.32 * (cool_rate - 2.1)
        - 0.016 * (humidity - 48)
        + line_offset
        + rng.normal(0, 0.62, n)
    )

    df = pd.DataFrame(
        {
            "Temperature": np.round(temperature, 2),
            "Pressure": np.round(pressure, 3),
            "TimeSec": np.round(time_sec, 1),
            "AdditivePct": np.round(additive, 3),
            "PreheatMin": np.round(preheat, 1),
            "CoolRate": np.round(cool_rate, 3),
            "LineId": line,
            "Humidity": np.round(humidity, 1),
            "TargetConductivity": np.round(conductivity, 3),
        }
    )

    missing_idx = rng.choice(n, size=max(4, n // 28), replace=False)
    hole_cols = ["Humidity", "Pressure", "TimeSec", "AdditivePct", "PreheatMin"]
    for i, col in zip(missing_idx, hole_cols * n):
        df.loc[i, col] = pd.NA
    return df


def write_sample_csv(path: Path, n: int = 200) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    generate_process_quality(n).to_csv(path, index=False)
    return path


def sample_okng_csv_path() -> Path:
    env = Path(__import__("os").environ.get("KEYFACTOR_SAMPLE_OKNG_CSV", ""))
    if env and env.is_file():
        return env
    here = Path(__file__).resolve()
    candidates = [
        here.parents[2] / "samples" / "process-okng.csv",
        here.parents[1] / "samples" / "process-okng.csv",
        Path.cwd() / "samples" / "process-okng.csv",
    ]
    for path in candidates:
        if path.is_file():
            return path
    return candidates[0]


def generate_process_okng(n: int = 200, seed: int = 42) -> pd.DataFrame:
    import numpy as np

    rng = np.random.default_rng(seed)
    temperature = rng.normal(182, 14, n).clip(148, 218)
    pressure = rng.normal(2.2, 0.35, n).clip(1.2, 3.4)
    time_sec = rng.normal(45, 10, n).clip(15, 90)
    additive = rng.normal(2.6, 0.7, n).clip(0.5, 4.8)
    humidity = rng.normal(48, 9, n).clip(22, 78)
    preheat = rng.normal(18, 5, n).clip(6, 36)
    cool_rate = rng.normal(2.1, 0.4, n).clip(0.8, 3.6)
    line = rng.choice(["L1", "L2", "L3"], size=n, p=[0.42, 0.35, 0.23])
    line_offset = np.where(line == "L1", 0.12, np.where(line == "L2", 0.0, -0.10))

    latent = (
        0.22 * (temperature - 182)
        + 0.18 * (additive - 2.6)
        + 0.03 * (time_sec - 45)
        + 0.15 * (pressure - 2.2)
        + 0.04 * (preheat - 18)
        - 0.08 * (cool_rate - 2.1)
        - 0.006 * (humidity - 48)
        + line_offset
        + rng.normal(0, 0.55, n)
    )
    quality = np.where(latent > 0.0, "NG", "OK")

    df = pd.DataFrame(
        {
            "Temperature": np.round(temperature, 2),
            "Pressure": np.round(pressure, 3),
            "TimeSec": np.round(time_sec, 1),
            "AdditivePct": np.round(additive, 3),
            "PreheatMin": np.round(preheat, 1),
            "CoolRate": np.round(cool_rate, 3),
            "LineId": line,
            "Humidity": np.round(humidity, 1),
            "QualityResult": quality,
        }
    )
    missing_idx = rng.choice(n, size=max(4, n // 28), replace=False)
    hole_cols = ["Humidity", "Pressure", "TimeSec", "AdditivePct", "PreheatMin"]
    for i, col in zip(missing_idx, hole_cols * n):
        df.loc[i, col] = pd.NA
    return df


def write_sample_okng_csv(path: Path, n: int = 200) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    generate_process_okng(n).to_csv(path, index=False)
    return path
