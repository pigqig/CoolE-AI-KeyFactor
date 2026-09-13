import pandas as pd
from fastapi.testclient import TestClient

from app.csv_loader import generate_process_okng, load_frame, sample_okng_csv_path, load_csv_path
from app.errors import ServiceError
from app.explainer import compute_dependence, compute_importances, compute_whatif
from app.main import app
from app.trainer import fit_model


def _binary_frame():
    df = pd.DataFrame(
        {
            "Temperature": [150, 155, 160, 165, 200, 205, 210, 215, 152, 158, 202, 208],
            "Pressure": [2.0, 2.1, 2.0, 2.2, 2.1, 2.0, 2.2, 2.1, 2.0, 2.1, 2.0, 2.2],
            "QualityResult": ["OK", "OK", "OK", "OK", "NG", "NG", "NG", "NG", "OK", "OK", "NG", "NG"],
        }
    )
    return load_frame(df)


def _multiclass_frame():
    temps = [150, 152, 155, 180, 182, 185, 210, 212, 215, 151, 183, 211]
    grades = ["A", "A", "A", "B", "B", "B", "C", "C", "C", "A", "B", "C"]
    df = pd.DataFrame(
        {
            "Temperature": temps,
            "Pressure": [2.0] * 12,
            "Grade": grades,
        }
    )
    return load_frame(df)


def test_binary_auto_still_classification():
    df, columns = _binary_frame()
    model = fit_model(df, columns, "QualityResult", "gbr")
    assert model["task"] == "classification"
    assert model["taskKind"] == "binary"
    assert set(model["classes"]) == {"OK", "NG"}
    metrics = model["metrics"]
    assert metrics["accuracy"] is not None
    assert metrics["f1"] is not None
    cm = metrics["confusion"]
    assert cm["labels"]
    assert len(cm["matrix"]) == len(cm["labels"]) == 2
    assert all(len(row) == 2 for row in cm["matrix"])


def test_multiclass_trains_as_classification_not_regression():
    df, columns = _multiclass_frame()
    model = fit_model(df, columns, "Grade", "gbr", task="multiclass")
    assert model["task"] == "classification"
    assert model["taskKind"] == "multiclass"
    assert len(model["classes"]) == 3
    assert model["metrics"]["task"] == "classification"
    assert model["metrics"]["rSquared"] is None
    assert model["metrics"]["accuracy"] is not None
    assert model["metrics"]["auc"] is None
    cm = model["metrics"]["confusion"]
    assert len(cm["labels"]) == 3
    assert len(cm["matrix"]) == 3
    assert all(len(row) == 3 for row in cm["matrix"])


def test_explicit_multiclass_on_binary_target_is_400():
    df, columns = _binary_frame()
    try:
        fit_model(df, columns, "QualityResult", "gbr", task="multiclass")
        raise AssertionError("expected ServiceError")
    except ServiceError as exc:
        assert exc.status_code == 400
        assert exc.error_code == "invalidMulticlassTarget"


def test_explicit_binary_on_multiclass_target_is_400():
    df, columns = _multiclass_frame()
    try:
        fit_model(df, columns, "Grade", "gbr", task="binary")
        raise AssertionError("expected ServiceError")
    except ServiceError as exc:
        assert exc.status_code == 400
        assert exc.error_code == "invalidBinaryTarget"


def test_sample_okng_loads_and_is_separable():
    path = sample_okng_csv_path()
    df, columns = load_csv_path(path)
    names = [c.name for c in columns]
    assert "QualityResult" in names
    assert "Temperature" in names
    assert "TargetConductivity" not in names
    assert 190 <= len(df) <= 220
    assert set(df["QualityResult"].dropna().astype(str).unique()) == {"OK", "NG"}

    generated, gcols = load_frame(generate_process_okng(160, seed=7))
    model = fit_model(generated, gcols, "QualityResult", "gbr", task="binary")
    ranked = compute_importances(model)
    assert ranked["importances"][0]["feature"] == "Temperature"
    assert ranked["classInsights"]
    assert {item["args"]["label"] for item in ranked["classInsights"]} == {"OK", "NG"}


def test_sample_okng_http_endpoints():
    client = TestClient(app)
    for url in ("/datasets/sample-okng", "/datasets/sample?kind=okng"):
        res = client.post(url)
        assert res.status_code == 200, url
        body = res.json()
        names = [c["name"] for c in body["columns"]]
        assert "QualityResult" in names


def test_whatif_classification_has_probabilities_and_class():
    df, columns = _binary_frame()
    model = fit_model(df, columns, "QualityResult", "gbr")
    high = compute_whatif(model, {"rowIndex": 0, "edits": {"Temperature": 215}})
    assert high["probabilities"]
    assert high["predictedClass"]
    assert high["narrative"]["insightCode"] in {"whatIfClass", "whatIfClassNoEdits"}
    assert high["narrative"]["summary"]


def test_dependence_classification_uses_class_probability():
    df, columns = _binary_frame()
    model = fit_model(df, columns, "QualityResult", "gbr")
    dep = compute_dependence(model, "Temperature", class_name="NG")
    assert dep["classLabel"] == "NG"
    assert dep["pdp"]
    assert all("y" in p for p in dep["pdp"])
