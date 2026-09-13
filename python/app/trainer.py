from __future__ import annotations

from typing import Any, Literal

import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.ensemble import (
    GradientBoostingClassifier,
    GradientBoostingRegressor,
    RandomForestClassifier,
    RandomForestRegressor,
)
from sklearn.impute import SimpleImputer
from sklearn.metrics import (
    accuracy_score,
    confusion_matrix,
    f1_score,
    mean_absolute_error,
    mean_squared_error,
    precision_recall_fscore_support,
    r2_score,
    roc_auc_score,
)
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder

from app.csv_loader import ColumnInfo, validate_target
from app.errors import ServiceError

TaskType = Literal["regression", "classification"]
TaskKind = Literal["regression", "binary", "multiclass"]


def infer_task(series: pd.Series) -> TaskType:
    return "classification" if series.dropna().nunique() == 2 else "regression"


def resolve_task(series: pd.Series, requested: str | None = "auto") -> tuple[TaskType, TaskKind]:
    raw = (requested or "auto").strip().lower()
    n = int(series.dropna().nunique())
    if raw in {"", "auto"}:
        if n == 2:
            return "classification", "binary"
        return "regression", "regression"
    if raw == "regression":
        return "regression", "regression"
    if raw == "binary":
        if n != 2:
            raise ServiceError(
                "invalidBinaryTarget",
                f"Binary classification needs exactly 2 target values; found {n}.",
                400,
            )
        return "classification", "binary"
    if raw == "multiclass":
        if n < 3:
            raise ServiceError(
                "invalidMulticlassTarget",
                f"Multiclass needs at least 3 target values; found {n}.",
                400,
            )
        return "classification", "multiclass"
    raise ServiceError("invalidTask", "task must be auto, regression, binary, or multiclass.", 400)


def feature_frame(df: pd.DataFrame, target: str) -> tuple[pd.DataFrame, pd.Series, list[str], list[str]]:
    validate_target(df, target)
    y = df[target]
    X = df.drop(columns=[target])
    if X.shape[1] == 0:
        raise ServiceError("noFeatures", "Need at least one feature column besides the target.", 400)
    numeric = [c for c in X.columns if pd.api.types.is_numeric_dtype(X[c])]
    categorical = [c for c in X.columns if c not in numeric]
    return X, y, numeric, categorical


def build_pipeline(
    numeric_cols: list[str],
    categorical_cols: list[str],
    task: TaskType,
    algorithm: str = "gbr",
    n_samples: int = 200,
) -> Pipeline:
    transformers: list[Any] = []
    if numeric_cols:
        transformers.append(
            (
                "num",
                Pipeline([("imputer", SimpleImputer(strategy="median"))]),
                numeric_cols,
            )
        )
    if categorical_cols:
        transformers.append(
            (
                "cat",
                Pipeline(
                    [
                        ("imputer", SimpleImputer(strategy="most_frequent")),
                        (
                            "oh",
                            OneHotEncoder(handle_unknown="ignore", sparse_output=False),
                        ),
                    ]
                ),
                categorical_cols,
            )
        )
    if not transformers:
        raise ServiceError("noFeatures", "No usable feature columns.", 400)

    pre = ColumnTransformer(transformers, remainder="drop")
    small = n_samples < 40
    n_estimators = 40 if small else 80
    max_depth = 2 if small else 3
    algo = (algorithm or "gbr").lower()

    if task == "classification":
        if algo == "rf":
            model = RandomForestClassifier(
                n_estimators=n_estimators,
                random_state=42,
                min_samples_leaf=1 if small else 2,
            )
        else:
            model = GradientBoostingClassifier(
                random_state=42,
                n_estimators=n_estimators,
                max_depth=max_depth,
            )
    else:
        if algo == "rf":
            model = RandomForestRegressor(
                n_estimators=n_estimators,
                random_state=42,
                min_samples_leaf=1 if small else 2,
            )
        else:
            model = GradientBoostingRegressor(
                random_state=42,
                n_estimators=n_estimators,
                max_depth=max_depth,
            )
    return Pipeline([("pre", pre), ("model", model)])


def _encode_labels(y: pd.Series) -> tuple[np.ndarray, list[Any]]:
    values = y.astype("string").fillna("__NA__")
    classes = sorted(values.unique().tolist())
    mapping = {label: i for i, label in enumerate(classes)}
    encoded = values.map(mapping).to_numpy(dtype=int)
    return encoded, classes


def evaluate(pipeline: Pipeline, X: pd.DataFrame, y: pd.Series, task: TaskType) -> dict[str, Any]:
    if task == "regression":
        y_true = pd.to_numeric(y, errors="coerce")
        pred = pipeline.predict(X)
        mask = y_true.notna()
        yt = y_true[mask].to_numpy(dtype=float)
        yp = np.asarray(pred)[mask.to_numpy()]
        rmse = float(np.sqrt(mean_squared_error(yt, yp))) if len(yt) else None
        return {
            "task": "regression",
            "rSquared": float(r2_score(yt, yp)) if len(yt) > 1 else None,
            "rmse": rmse,
            "mae": float(mean_absolute_error(yt, yp)) if len(yt) else None,
            "accuracy": None,
            "auc": None,
            "f1": None,
        }

    pred = pipeline.predict(X)
    y_true = np.asarray(y)
    acc = float(accuracy_score(y_true, pred))
    n_labels = int(len(np.unique(y_true)))
    kind: TaskKind = "binary" if n_labels == 2 else "multiclass"
    auc = None
    if kind == "binary":
        try:
            proba = pipeline.predict_proba(X)
            auc = float(roc_auc_score(y_true, proba[:, 1]))
        except Exception:  # noqa: BLE001
            auc = None
    try:
        f1 = float(f1_score(y_true, pred, average="binary" if kind == "binary" else "macro"))
    except Exception:  # noqa: BLE001
        f1 = None
    return {
        "task": "classification",
        "taskKind": kind,
        "rSquared": None,
        "rmse": None,
        "mae": None,
        "accuracy": acc,
        "auc": auc,
        "f1": f1,
    }


def confusion_payload(y_true: np.ndarray, y_pred: np.ndarray, classes: list[Any]) -> dict[str, Any]:
    labels = list(range(len(classes)))
    matrix = confusion_matrix(y_true, y_pred, labels=labels)
    return {
        "labels": [str(c) for c in classes],
        "matrix": matrix.astype(int).tolist(),
    }


def class_metric_rows(y_true: np.ndarray, y_pred: np.ndarray, classes: list[Any]) -> list[dict[str, Any]]:
    labels = list(range(len(classes)))
    prec, rec, _, supp = precision_recall_fscore_support(
        y_true, y_pred, labels=labels, zero_division=0
    )
    total = max(int(len(y_true)), 1)
    rows = []
    for i, label in enumerate(classes):
        rows.append(
            {
                "label": str(label),
                "support": int(supp[i]),
                "supportPct": round(100.0 * float(supp[i]) / total, 1),
                "precision": round(float(prec[i]) * 100.0, 1),
                "recall": round(float(rec[i]) * 100.0, 1),
            }
        )
    return rows


def fit_model(
    df: pd.DataFrame,
    columns: list[ColumnInfo],
    target: str,
    algorithm: str = "gbr",
    task: str | None = "auto",
) -> dict[str, Any]:
    X, y_raw, numeric, categorical = feature_frame(df, target)
    usable = y_raw.notna()
    X = X.loc[usable].reset_index(drop=True)
    y_raw = y_raw.loc[usable].reset_index(drop=True)
    if len(X) < 8:
        raise ServiceError("tooFewRows", "Need at least 8 rows with a usable target value.", 400)

    task_type, task_kind = resolve_task(y_raw, task)
    classes: list[Any] | None = None
    if task_type == "classification":
        y, classes = _encode_labels(y_raw)
        y = pd.Series(y)
    else:
        y = pd.to_numeric(y_raw, errors="coerce")
        mask = y.notna()
        X = X.loc[mask].reset_index(drop=True)
        y = y.loc[mask].reset_index(drop=True)

    pipeline = build_pipeline(numeric, categorical, task_type, algorithm, n_samples=len(X))

    split_kw: dict[str, Any] = {"test_size": 0.25, "random_state": 42}
    if task_type == "classification":
        counts = y.value_counts()
        if len(X) >= 24 and int(counts.min()) >= 2:
            split_kw["stratify"] = y

    if len(X) >= 24:
        X_train, X_test, y_train, y_test = train_test_split(X, y, **split_kw)
    else:
        X_train, X_test, y_train, y_test = X, X, y, y

    pipeline.fit(X_train, y_train)
    metrics = evaluate(pipeline, X_test, y_test, task_type)
    train_metrics = evaluate(pipeline, X_train, y_train, task_type)
    metrics["trainRSquared"] = train_metrics.get("rSquared")
    metrics["trainAccuracy"] = train_metrics.get("accuracy")
    metrics["algorithm"] = "random_forest" if algorithm == "rf" else "gradient_boosting"
    metrics["nRows"] = int(len(X))
    metrics["nFeatures"] = int(X.shape[1])
    metrics["targetColumn"] = target
    metrics["taskKind"] = task_kind
    if task_type == "classification" and classes is not None:
        y_hat = pipeline.predict(X_test)
        metrics["confusion"] = confusion_payload(np.asarray(y_test), np.asarray(y_hat), classes)
        metrics["classMetrics"] = class_metric_rows(np.asarray(y_test), np.asarray(y_hat), classes)
        metrics["classes"] = [str(c) for c in classes]
        if task_kind != "binary":
            metrics["auc"] = None

    predictions = predict_values(pipeline, X, task_type, classes)
    return {
        "pipeline": pipeline,
        "X": X,
        "y": y,
        "y_raw": y_raw if task_type == "classification" else y,
        "task": task_type,
        "taskKind": task_kind,
        "classes": classes,
        "numeric": numeric,
        "categorical": categorical,
        "metrics": metrics,
        "predictions": predictions,
        "target": target,
        "algorithm": metrics["algorithm"],
        "source_index": usable[usable].index.to_list() if hasattr(usable, "index") else list(range(len(X))),
    }


def default_class_index(classes: list[Any] | None, proba_width: int) -> int:
    if classes:
        return max(len(classes) - 1, 0)
    return max(proba_width - 1, 0)


def predict_values(
    pipeline: Pipeline,
    X: pd.DataFrame,
    task: TaskType,
    classes: list[Any] | None = None,
    class_index: int | None = None,
) -> np.ndarray:
    if task == "classification":
        proba = pipeline.predict_proba(X)
        if proba.ndim == 2 and proba.shape[1] >= 2:
            idx = default_class_index(classes, proba.shape[1]) if class_index is None else class_index
            idx = min(max(idx, 0), proba.shape[1] - 1)
            return proba[:, idx]
        return proba.ravel()
    return np.asarray(pipeline.predict(X), dtype=float)


def predict_one(
    pipeline: Pipeline,
    row: pd.DataFrame,
    task: TaskType,
    classes: list[Any] | None = None,
    class_index: int | None = None,
) -> float:
    values = predict_values(pipeline, row, task, classes, class_index)
    return float(values[0])
