from __future__ import annotations

from typing import Any

import numpy as np
import pandas as pd


def _finite(value: float | None) -> float | None:
    if value is None:
        return None
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    if not np.isfinite(number):
        return None
    return number


def direction_from_series(x: pd.Series, y: pd.Series) -> tuple[str | None, float | None]:
    xv = pd.to_numeric(x, errors="coerce")
    yv = pd.to_numeric(y, errors="coerce")
    mask = xv.notna() & yv.notna()
    if int(mask.sum()) < 5:
        return None, None
    corr = float(np.corrcoef(xv[mask], yv[mask])[0, 1])
    if not np.isfinite(corr):
        return None, None
    if corr > 0.08:
        return "up", corr
    if corr < -0.08:
        return "down", corr
    return "flat", corr


def direction_from_pdp(xs: list[float], ys: list[float]) -> tuple[str | None, float | None]:
    if len(xs) < 2 or len(ys) < 2:
        return None, None
    slope = (ys[-1] - ys[0]) / (xs[-1] - xs[0] + 1e-9)
    if slope > 1e-6:
        return "up", slope
    if slope < -1e-6:
        return "down", slope
    return "flat", slope


def steepest_range(xs: list[float], ys: list[float]) -> tuple[float | None, float | None]:
    if len(xs) < 2:
        return None, None
    arr_x = np.asarray(xs, dtype=float)
    arr_y = np.asarray(ys, dtype=float)
    dx = np.diff(arr_x)
    dy = np.diff(arr_y)
    with np.errstate(divide="ignore", invalid="ignore"):
        slopes = np.abs(dy / np.where(np.abs(dx) < 1e-9, np.nan, dx))
    if not np.isfinite(slopes).any():
        return float(arr_x[0]), float(arr_x[-1])
    i = int(np.nanargmax(slopes))
    return float(arr_x[i]), float(arr_x[i + 1])


def dataset_overview(
    df: pd.DataFrame,
    columns: list[Any],
    target: str | None = None,
) -> dict[str, Any]:
    numeric_count = sum(1 for c in columns if getattr(c, "type", c.get("type") if isinstance(c, dict) else None) == "numeric")
    categorical_count = len(columns) - numeric_count
    missing: dict[str, int] = {}
    for col in df.columns:
        count = int(df[col].isna().sum())
        if count:
            missing[str(col)] = count

    missing_notes = [f"{name}:{count}" for name, count in missing.items()]
    args = {
        "rowCount": int(len(df)),
        "columnCount": int(df.shape[1]),
        "numericCount": numeric_count,
        "categoricalCount": categorical_count,
        "target": target or "",
        "missingCount": sum(missing.values()),
        "missingColumns": ", ".join(missing.keys()) if missing else "",
    }
    if target:
        summary = (
            f"This dataset has {args['rowCount']} process records and {args['columnCount']} columns. "
            f"The quality target is '{target}'. The next step is to rank which process settings best explain changes in that target."
        )
        code = "datasetOverviewWithTarget"
    else:
        summary = (
            f"This dataset has {args['rowCount']} process records and {args['columnCount']} parameters "
            f"({numeric_count} numeric, {categorical_count} categorical)."
        )
        code = "datasetOverview"
    if missing:
        summary += f" Missing values were found in: {args['missingColumns']}."

    return {
        "rowCount": args["rowCount"],
        "columnCount": args["columnCount"],
        "numericCount": numeric_count,
        "categoricalCount": categorical_count,
        "missingCounts": missing,
        "missingNotes": missing_notes,
        "targetColumn": target,
        "insightCode": code,
        "summary": summary,
        "args": args,
    }


def importance_insights(
    ranked: list[dict[str, Any]],
    target: str,
) -> list[dict[str, Any]]:
    insights: list[dict[str, Any]] = []
    for item in ranked:
        rank = int(item["rank"])
        share_pct = round(float(item["share"]) * 100, 1)
        direction = item.get("direction")
        args = {
            "name": item["feature"],
            "rank": rank,
            "share": share_pct,
            "target": target,
            "importance": round(float(item["importance"]), 4),
        }
        if rank == 1:
            code = "topFactor"
            summary = (
                f"{args['name']} is the strongest driver of {target} "
                f"(about {share_pct}% of explained importance). Align this setting first when remanufacturing."
            )
        elif rank <= 3:
            code = "highFactor"
            summary = (
                f"{args['name']} is a top-{rank} factor for {target} "
                f"(about {share_pct}%). Keep it in the control plan."
            )
        else:
            code = "otherFactor"
            summary = (
                f"{args['name']} has a smaller share ({share_pct}%) of the model's explanation for {target}."
            )

        if direction == "up":
            summary += f" Higher {args['name']} tends to raise predicted {target}."
        elif direction == "down":
            summary += f" Higher {args['name']} tends to lower predicted {target}."

        insights.append(
            {
                "feature": item["feature"],
                "rank": rank,
                "share": float(item["share"]),
                "importance": float(item["importance"]),
                "direction": direction,
                "correlation": _finite(item.get("correlation")),
                "insightCode": code,
                "summary": summary,
                "args": args,
            }
        )
    return insights


def dependence_insight(
    feature: str,
    target: str,
    xs: list[float],
    ys: list[float],
    feature_type: str,
) -> dict[str, Any]:
    direction, slope = direction_from_pdp(xs, ys) if feature_type == "numeric" else (None, None)
    lo, hi = steepest_range(xs, ys) if feature_type == "numeric" else (None, None)
    args = {
        "feature": feature,
        "target": target,
        "direction": direction or "flat",
        "steepestFrom": None if lo is None else round(lo, 3),
        "steepestTo": None if hi is None else round(hi, 3),
    }
    if feature_type == "categorical":
        summary = (
            f"The chart compares average predicted {target} for each {feature} group. "
            "Use it to see which line or category lands closer to the quality target."
        )
        code = "dependenceCategorical"
    else:
        summary = (
            f"The X axis is {feature}. The curve is the average predicted {target} "
            f"when only this setting is changed. "
        )
        if lo is not None and hi is not None:
            summary += f"The target changes most between {args['steepestFrom']} and {args['steepestTo']}."
        code = "dependenceNumeric"
        if direction == "up":
            summary += f" Higher {feature} tends to increase predicted {target}."
        elif direction == "down":
            summary += f" Higher {feature} tends to decrease predicted {target}."

    return {
        "insightCode": code,
        "summary": summary,
        "direction": direction,
        "slope": _finite(slope),
        "steepestFrom": args["steepestFrom"],
        "steepestTo": args["steepestTo"],
        "args": args,
    }


def whatif_narrative(
    target: str,
    baseline: float,
    prediction: float,
    edits: dict[str, Any],
    original: dict[str, Any],
    ranks: dict[str, int],
) -> dict[str, Any]:
    delta = float(prediction) - float(baseline)
    denom = abs(float(baseline)) if abs(float(baseline)) > 1e-9 else None
    delta_pct = None if denom is None else round(100.0 * delta / denom, 1)

    edit_bits = []
    primary = None
    best_rank = 10_000
    for key, value in edits.items():
        before = original.get(key)
        edit_bits.append(f"{key} from {before} to {value}")
        rank = ranks.get(key, 10_000)
        if rank < best_rank:
            best_rank = rank
            primary = key

    if not edit_bits:
        summary = (
            f"No settings were changed. Predicted {target} stays at {round(prediction, 3)}, "
            "which is this row's baseline."
        )
        code = "whatIfNoEdits"
    else:
        sign = f"{delta_pct:+.1f}%" if delta_pct is not None else f"{delta:+.3f}"
        summary = (
            f"Changing {'; '.join(edit_bits)} moved predicted {target} "
            f"from {round(baseline, 3)} to {round(prediction, 3)} ({sign})."
        )
        code = "whatIfChanged"
        if primary and best_rank < 10_000:
            summary += f" {primary} is rank {best_rank} among key factors."

    args = {
        "target": target,
        "baseline": round(float(baseline), 4),
        "prediction": round(float(prediction), 4),
        "delta": round(delta, 4),
        "deltaPct": delta_pct,
        "edits": "; ".join(edit_bits),
        "primaryFeature": primary or "",
        "primaryRank": None if best_rank == 10_000 else best_rank,
        "editCount": len(edits),
    }
    return {
        "insightCode": code,
        "summary": summary,
        "delta": delta,
        "deltaPct": delta_pct,
        "args": args,
    }


def class_insights(
    rows: list[dict[str, Any]],
    push: list[dict[str, Any]],
    target: str,
) -> list[dict[str, Any]]:
    by_label = {item["label"]: item for item in push}
    insights: list[dict[str, Any]] = []
    for row in rows:
        label = str(row["label"])
        extra = by_label.get(label, {})
        feature = extra.get("feature") or ""
        direction = extra.get("direction") or "flat"
        args = {
            "label": label,
            "target": target,
            "support": row["support"],
            "supportPct": row["supportPct"],
            "precision": row["precision"],
            "recall": row["recall"],
            "feature": feature,
            "direction": direction,
        }
        summary = (
            f"Class {label} has {row['support']} rows ({row['supportPct']}%). "
            f"Precision {row['precision']}%, recall {row['recall']}%."
        )
        if feature:
            if direction == "up":
                summary += f" Higher {feature} tends to raise the chance of {label}."
            elif direction == "down":
                summary += f" Higher {feature} tends to lower the chance of {label}."
            else:
                summary += f" The strongest global factor is {feature}; this is not a per-class SHAP score."
        insights.append(
            {
                "feature": feature or None,
                "insightCode": "classCard",
                "summary": summary,
                "args": args,
                "direction": direction,
            }
        )
    return insights


def whatif_class_narrative(
    target: str,
    label: str,
    probability: float,
    edits: dict[str, Any],
    original: dict[str, Any],
    ranks: dict[str, int],
) -> dict[str, Any]:
    pct = round(float(probability) * 100.0, 1)
    primary = None
    best_rank = 10_000
    hint = ""
    hintKey = ""
    for key, value in edits.items():
        rank = ranks.get(key, 10_000)
        if rank < best_rank:
            best_rank = rank
            primary = key
            before = original.get(key)
            try:
                if float(value) > float(before):  # type: ignore[arg-type]
                    hint = "higher"
                    hintKey = "higher"
                elif float(value) < float(before):  # type: ignore[arg-type]
                    hint = "lower"
                    hintKey = "lower"
                else:
                    hint = "changed"
                    hintKey = "changed"
            except (TypeError, ValueError):
                hint = "changed"
                hintKey = "changed"

    if not edits:
        code = "whatIfClassNoEdits"
        summary = f"No settings were changed. This row still looks like {label} ({pct}%)."
    else:
        code = "whatIfClass"
        extra = f", mainly because {primary} is {hint}" if primary else ""
        summary = f"These settings look more like {label} ({pct}% probability){extra}."

    args = {
        "target": target,
        "label": label,
        "probability": pct,
        "feature": primary or "",
        "hint": hint,
        "hintKey": hintKey,
        "editCount": len(edits),
    }
    return {
        "insightCode": code,
        "summary": summary,
        "args": args,
    }
