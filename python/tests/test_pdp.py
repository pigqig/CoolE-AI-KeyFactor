import pandas as pd

from app.csv_loader import load_frame
from app.explainer import compute_dependence
from app.trainer import fit_model


def test_pdp_increases_for_linear_target():
    x = list(range(16))
    df = pd.DataFrame(
        {
            "x": x,
            "noise": [0.01 * ((i * 3) % 5) for i in x],
            "y": [2 * v + 0.02 * ((v * 3) % 5) for v in x],
        }
    )
    df, columns = load_frame(df)
    model = fit_model(df, columns, "y", "gbr")
    dep = compute_dependence(model, "x")
    ys = [p["y"] for p in dep["pdp"]]
    xs = [p["x"] for p in dep["pdp"]]
    assert len(ys) >= 3
    assert ys[-1] > ys[0]
    # roughly follows y = 2x
    slope = (ys[-1] - ys[0]) / (xs[-1] - xs[0])
    assert slope > 0.5
    assert dep["insight"]["summary"]
    assert dep["insight"]["direction"] in {"up", "flat"}
