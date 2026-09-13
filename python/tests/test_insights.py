from app.csv_loader import generate_process_quality, load_frame
from app.explainer import compute_importances
from app.insights import dataset_overview
from app.trainer import fit_model


def test_importances_include_non_empty_insight_summary():
    df, columns = load_frame(generate_process_quality(120, seed=3))
    model = fit_model(df, columns, "TargetConductivity")
    result = compute_importances(model)
    assert result["insights"]
    first = result["insights"][0]
    assert first["summary"]
    assert first["insightCode"]
    assert first["share"] >= 0
    assert first["rank"] == 1


def test_repo_sample_ranks_temperature_first():
    from app.csv_loader import load_csv_path, sample_csv_path

    df, columns = load_csv_path(sample_csv_path())
    model = fit_model(df, columns, "TargetConductivity")
    result = compute_importances(model)
    assert result["importances"][0]["feature"] == "Temperature"
    assert result["insights"][0]["summary"]
    assert result["insights"][0]["share"] >= 0.25
    assert result["importances"][1]["importance"] > 0


def test_dataset_overview_mentions_counts():
    df, columns = load_frame(generate_process_quality(40))
    overview = dataset_overview(df, columns, "TargetConductivity")
    assert overview["rowCount"] == 40
    assert "TargetConductivity" in overview["summary"]
    assert overview["insightCode"]
