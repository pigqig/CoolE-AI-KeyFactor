import pandas as pd

from app.csv_loader import generate_process_quality, load_frame
from app.explainer import compute_importances
from app.trainer import fit_model


def test_sample_model_finite_metrics_and_strong_feature():
    df, columns = load_frame(generate_process_quality(160, seed=7))
    model = fit_model(df, columns, "TargetConductivity", "gbr")
    metrics = model["metrics"]
    assert metrics["task"] == "regression"
    assert metrics["rSquared"] is not None and metrics["rSquared"] == metrics["rSquared"]
    assert metrics["rmse"] is not None and metrics["rmse"] >= 0

    result = compute_importances(model)
    values = [item["importance"] for item in result["importances"]]
    assert values
    assert all(v == v and v >= 0 for v in values)
    assert result["insights"]
    assert all(item["summary"] for item in result["insights"])

    names = [item["feature"] for item in result["importances"]]
    assert names[0] == "Temperature"
    assert result["insights"][0]["share"] >= 0.25
    assert result["importances"][1]["share"] >= 0.05


def test_tiny_inline_table_trains():
    df = pd.DataFrame(
        {
            "strong": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
            "weak": [0.1, -0.2, 0.05, 0.0, 0.2, -0.1, 0.15, 0.05, -0.05, 0.1, 0.0, -0.15],
            "y": [2.1, 3.9, 6.2, 8.0, 9.8, 12.2, 13.9, 16.1, 18.2, 19.7, 22.1, 23.8],
        }
    )
    df, columns = load_frame(df)
    model = fit_model(df, columns, "y", "rf")
    assert model["metrics"]["rmse"] == model["metrics"]["rmse"]
    ranked = compute_importances(model)["importances"]
    assert ranked[0]["feature"] == "strong" or "strong" in [r["feature"] for r in ranked[:2]]


def test_skips_id_like_categorical_and_trains_with_missing_cats():
    n = 40
    df = pd.DataFrame(
        {
            "Temp": list(range(n)),
            "LotNo": [f"LOT-{i}" for i in range(n)],
            "Line": (["L1", "L2", None] * 14)[:n],
            "y": [float(i) * 0.5 + 1.0 for i in range(n)],
        }
    )
    df, columns = load_frame(df)
    model = fit_model(df, columns, "y", "rf")
    assert "LotNo" not in model["categorical"]
    assert "LotNo" not in model["X"].columns
    assert "Line" in model["categorical"]
    assert model["metrics"]["rmse"] == model["metrics"]["rmse"]
