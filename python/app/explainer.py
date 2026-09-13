from __future__ import annotations

from typing import Any

import numpy as np
import pandas as pd
from sklearn.inspection import partial_dependence, permutation_importance

from app.errors import ServiceError
from app.insights import (
    class_insights,
    dependence_insight,
    direction_from_pdp,
    direction_from_series,
    importance_insights,
    whatif_class_narrative,
    whatif_narrative,
)
from app.trainer import class_metric_rows, default_class_index, predict_one, predict_values


def _nonneg(values: np.ndarray) -> np.ndarray:
    cleaned = np.asarray(values, dtype=float)
    cleaned = np.where(np.isfinite(cleaned), cleaned, 0.0)
    return np.clip(cleaned, 0.0, None)


def compute_importances(model: dict[str, Any], top_n: int | None = None) -> dict[str, Any]:
    pipeline = model["pipeline"]
    X: pd.DataFrame = model["X"]
    y = model["y"]
    task = model["task"]
    target = model["target"]
    scoring = "r2" if task == "regression" else "accuracy"
    repeats = 4 if len(X) < 40 else 8

    result = permutation_importance(
        pipeline,
        X,
        y,
        n_repeats=repeats,
        random_state=42,
        scoring=scoring,
    )
    means = _nonneg(result.importances_mean)
    preds = np.asarray(model["predictions"], dtype=float)
    total = float(means.sum()) or 1.0

    ranked: list[dict[str, Any]] = []
    for name, importance in zip(X.columns, means):
        series = X[name]
        if pd.api.types.is_numeric_dtype(series):
            direction, corr = direction_from_series(series, pd.Series(preds))
        else:
            direction, corr = _categorical_direction(series, preds)
        ranked.append(
            {
                "feature": str(name),
                "importance": float(importance),
                "share": float(importance) / total,
                "direction": direction,
                "correlation": corr,
            }
        )
    ranked.sort(key=lambda item: item["importance"], reverse=True)
    for i, item in enumerate(ranked, start=1):
        item["rank"] = i
    if top_n:
        ranked = ranked[: max(1, int(top_n))]

    insights = importance_insights(ranked, target)
    class_cards: list[dict[str, Any]] = []
    if task == "classification" and model.get("classes"):
        classes = list(model["classes"])
        y_enc = np.asarray(y)
        pred = np.asarray(pipeline.predict(X))
        metric_rows = model.get("metrics", {}).get("classMetrics") or class_metric_rows(y_enc, pred, classes)
        push: list[dict[str, Any]] = []
        try:
            proba = pipeline.predict_proba(X)
        except Exception:  # noqa: BLE001
            proba = None
        top_feat = ranked[0]["feature"] if ranked else ""
        for i, label in enumerate(classes):
            direction, corr = None, None
            if proba is not None and top_feat and top_feat in X.columns and pd.api.types.is_numeric_dtype(X[top_feat]):
                direction, corr = direction_from_series(X[top_feat], pd.Series(proba[:, i]))
            push.append({"label": str(label), "feature": top_feat, "direction": direction or "flat", "correlation": corr})
        class_cards = class_insights(metric_rows, push, target)

    return {
        "method": "permutation",
        "targetColumn": target,
        "importances": ranked,
        "insights": insights,
        "classInsights": class_cards,
    }


def _categorical_direction(series: pd.Series, preds: np.ndarray) -> tuple[str | None, float | None]:
    frame = pd.DataFrame({"cat": series.astype("string"), "y": preds}).dropna()
    if frame.empty or frame["cat"].nunique() < 2:
        return None, None
    means = frame.groupby("cat")["y"].mean().sort_values()
    delta = float(means.iloc[-1] - means.iloc[0])
    if abs(delta) < 1e-6:
        return "flat", delta
    return "up" if delta > 0 else "down", delta


def _resolve_class_index(model: dict[str, Any], class_name: str | None) -> tuple[int, str | None]:
    classes = [str(c) for c in (model.get("classes") or [])]
    if not classes:
        return 0, None
    if class_name:
        if class_name in classes:
            return classes.index(class_name), class_name
        try:
            idx = int(class_name)
            if 0 <= idx < len(classes):
                return idx, classes[idx]
        except ValueError:
            pass
    idx = default_class_index(classes, len(classes))
    return idx, classes[idx]


def _pdp_curve(pipeline, X: pd.DataFrame, feature: str, task: str, class_index: int) -> tuple[np.ndarray, np.ndarray]:
    kwargs: dict[str, Any] = {
        "kind": "average",
        "grid_resolution": 16 if len(X) < 80 else 24,
    }
    if task == "classification":
        kwargs["response_method"] = "predict_proba"
    pd_result = partial_dependence(pipeline, X, [feature], **kwargs)
    grid = np.asarray(pd_result["grid_values"][0])
    average = np.asarray(pd_result["average"])
    if average.ndim == 3:
        average = average[0]
    if average.ndim == 2:
        if average.shape[0] == 1:
            curve = average[0]
        else:
            idx = min(max(class_index, 0), average.shape[0] - 1)
            curve = average[idx]
    else:
        curve = average.ravel()
    return grid, np.asarray(curve).ravel()


def compute_dependence(
    model: dict[str, Any],
    feature: str,
    color_by: str | None = None,
    class_name: str | None = None,
) -> dict[str, Any]:
    pipeline = model["pipeline"]
    X: pd.DataFrame = model["X"]
    target = model["target"]
    task = model["task"]
    if feature not in X.columns:
        raise ServiceError("unknownFeature", f"Feature '{feature}' was not found.", 400)

    feature_type = "numeric" if pd.api.types.is_numeric_dtype(X[feature]) else "categorical"
    class_index, class_label = _resolve_class_index(model, class_name)
    if task == "classification":
        preds = predict_values(pipeline, X, task, model.get("classes"), class_index)
    else:
        preds = np.asarray(model["predictions"], dtype=float)

    try:
        grid, average = _pdp_curve(pipeline, X, feature, task, class_index)
    except Exception as exc:  # noqa: BLE001
        raise ServiceError("dependenceFailed", f"Partial dependence failed: {exc}", 400) from exc

    pdp: list[dict[str, Any]] = []
    xs: list[float] = []
    ys: list[float] = []
    if feature_type == "numeric":
        for x_val, y_val in zip(grid, average):
            xs.append(float(x_val))
            ys.append(float(y_val))
            pdp.append({"x": float(x_val), "y": float(y_val), "xLabel": None})
    else:
        labels = [str(v) for v in grid]
        for i, (label, y_val) in enumerate(zip(labels, average)):
            xs.append(float(i))
            ys.append(float(y_val))
            pdp.append({"x": float(i), "y": float(y_val), "xLabel": label})

    points: list[dict[str, Any]] = []
    color_series = X[color_by] if color_by and color_by in X.columns else None
    for i in range(len(X)):
        raw = X.iloc[i][feature]
        if pd.isna(raw):
            continue
        if feature_type == "numeric":
            x_val = float(raw)
            x_label = None
        else:
            x_label = str(raw)
            x_val = float(next((p["x"] for p in pdp if p["xLabel"] == x_label), 0))
        color_val = None if color_series is None or pd.isna(color_series.iloc[i]) else color_series.iloc[i]
        if isinstance(color_val, (np.floating, float)):
            color_val = float(color_val)
        elif color_val is not None:
            color_val = str(color_val)
        points.append({"x": x_val, "y": float(preds[i]), "color": color_val, "xLabel": x_label})

    insight = dependence_insight(feature, target, xs, ys, feature_type)
    direction, _ = direction_from_pdp(xs, ys) if feature_type == "numeric" else (insight.get("direction"), None)
    insight["direction"] = direction or insight.get("direction")
    if class_label:
        insight.setdefault("args", {})["classLabel"] = class_label
        insight["insightCode"] = "dependenceClassProbability" if feature_type == "numeric" else insight["insightCode"]
        insight["summary"] = (
            f"The curve is the average predicted probability of {class_label} "
            f"when only {feature} is changed."
        )

    return {
        "feature": feature,
        "featureType": feature_type,
        "colorBy": color_by,
        "targetColumn": target,
        "classLabel": class_label,
        "pdp": pdp,
        "points": points,
        "insight": insight,
    }


def _as_row_frame(X: pd.DataFrame, values: dict[str, Any]) -> pd.DataFrame:
    row = {col: values.get(col) for col in X.columns}
    frame = pd.DataFrame([row], columns=list(X.columns))
    for col in X.columns:
        if pd.api.types.is_numeric_dtype(X[col]):
            frame[col] = pd.to_numeric(frame[col], errors="coerce")
        else:
            frame[col] = frame[col].astype("string")
    return frame


def compute_whatif(model: dict[str, Any], body: dict[str, Any]) -> dict[str, Any]:
    X: pd.DataFrame = model["X"]
    pipeline = model["pipeline"]
    task = model["task"]
    target = model["target"]
    edits = body.get("edits") or {}
    if not isinstance(edits, dict):
        raise ServiceError("invalidEdits", "edits must be an object of feature → value.", 400)

    if body.get("row") and isinstance(body["row"], dict):
        original = {col: body["row"].get(col) for col in X.columns}
        row_index = body.get("rowIndex")
    else:
        idx = int(body.get("rowIndex") or 0)
        if idx < 0 or idx >= len(X):
            raise ServiceError("rowNotFound", f"Row index {idx} is out of range.", 404)
        original = {col: _json_cell(X.iloc[idx][col]) for col in X.columns}
        row_index = idx

    edited = dict(original)
    applied: dict[str, Any] = {}
    for key, value in edits.items():
        if key not in X.columns:
            continue
        edited[key] = value
        applied[key] = value

    base_row = _as_row_frame(X, original)
    edit_row = _as_row_frame(X, edited)
    classes = list(model.get("classes") or [])
    predicted_class = None
    baseline_class = None
    focus_index = None
    if task == "classification":
        base_idx = int(pipeline.predict(base_row)[0])
        pred_idx = int(pipeline.predict(edit_row)[0])
        baseline_class = str(classes[base_idx]) if classes else str(base_idx)
        predicted_class = str(classes[pred_idx]) if classes else str(pred_idx)
        focus_index = pred_idx
        baseline = float(pipeline.predict_proba(base_row)[0][base_idx])
        prediction = float(pipeline.predict_proba(edit_row)[0][pred_idx])
        mean_prediction = float(np.mean(predict_values(pipeline, X, task, classes, pred_idx)))
    else:
        baseline = predict_one(pipeline, base_row, task)
        prediction = predict_one(pipeline, edit_row, task)
        mean_prediction = float(np.mean(model["predictions"]))

    contribs = []
    for col in X.columns:
        alt = dict(edited)
        if pd.api.types.is_numeric_dtype(X[col]):
            alt[col] = float(pd.to_numeric(X[col], errors="coerce").median())
        else:
            mode = X[col].mode(dropna=True)
            alt[col] = None if mode.empty else mode.iloc[0]
        alt_pred = predict_one(pipeline, _as_row_frame(X, alt), task, classes, focus_index)
        contribs.append(
            {
                "feature": col,
                "delta": float(prediction - alt_pred),
            }
        )
    contribs.sort(key=lambda item: abs(item["delta"]), reverse=True)

    ranks = {item["feature"]: item["rank"] for item in model.get("importance_cache", {}).get("importances", [])}
    if not ranks:
        # lightweight rank by |contribution|
        ordered = [c["feature"] for c in contribs]
        ranks = {name: i + 1 for i, name in enumerate(ordered)}

    if task == "classification":
        narrative = whatif_class_narrative(target, predicted_class or "", prediction, applied, original, ranks)
    else:
        narrative = whatif_narrative(target, baseline, prediction, applied, original, ranks)
    probabilities = None
    if task == "classification":
        proba = pipeline.predict_proba(edit_row)[0]
        labels = classes or [str(i) for i in range(len(proba))]
        probabilities = [
            {"label": str(labels[i]), "probability": float(p)} for i, p in enumerate(proba)
        ]

    return {
        "rowIndex": row_index,
        "task": task,
        "targetColumn": target,
        "baseline": baseline,
        "prediction": prediction,
        "predictedClass": predicted_class,
        "baselineClass": baseline_class,
        "meanPrediction": mean_prediction,
        "row": edited,
        "edits": applied,
        "contributions": contribs,
        "probabilities": probabilities,
        "narrative": narrative,
        "insight": narrative,
    }


def compute_rows(model: dict[str, Any], limit: int = 500) -> dict[str, Any]:
    X: pd.DataFrame = model["X"]
    y = model["y"]
    preds = np.asarray(model["predictions"], dtype=float)
    task = model["task"]
    rows = []
    n = min(len(X), limit)
    for i in range(n):
        actual = y.iloc[i]
        if task == "regression":
            actual_v = None if pd.isna(actual) else float(actual)
        else:
            classes = model.get("classes") or []
            actual_v = classes[int(actual)] if classes and not pd.isna(actual) else actual
        pred = float(preds[i])
        residual = None if actual_v is None or task != "regression" else pred - float(actual_v)
        cells = {col: _json_cell(X.iloc[i][col]) for col in X.columns}
        rows.append(
            {
                "index": i,
                "actual": actual_v if not isinstance(actual_v, (np.generic,)) else actual_v.item(),
                "predicted": pred,
                "residual": None if residual is None else float(residual),
                "values": cells,
            }
        )
    return {"rows": rows, "rowCount": int(len(X)), "task": task, "targetColumn": model["target"]}


def _json_cell(value: Any) -> Any:
    if value is None or (isinstance(value, float) and not np.isfinite(value)) or pd.isna(value):
        return None
    if isinstance(value, (np.floating, float)):
        return float(value)
    if isinstance(value, (np.integer, int)):
        return int(value)
    return str(value)
