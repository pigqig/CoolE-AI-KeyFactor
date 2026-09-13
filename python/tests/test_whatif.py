import pandas as pd

from app.csv_loader import load_frame
from app.explainer import compute_whatif
from app.trainer import fit_model


def test_whatif_moves_prediction_when_strong_feature_changes():
    df = pd.DataFrame(
        {
            "strong": [i * 1.0 for i in range(1, 21)],
            "weak": [0.1 * ((i * 2) % 3) for i in range(1, 21)],
            "y": [3 * i + 0.05 * ((i * 2) % 3) for i in range(1, 21)],
        }
    )
    df, columns = load_frame(df)
    model = fit_model(df, columns, "y", "gbr")
    low = compute_whatif(model, {"rowIndex": 2, "edits": {"strong": 2}})
    high = compute_whatif(model, {"rowIndex": 2, "edits": {"strong": 18}})
    assert high["prediction"] > low["prediction"]
    assert high["narrative"]["summary"]
    assert high["insight"]["summary"]
