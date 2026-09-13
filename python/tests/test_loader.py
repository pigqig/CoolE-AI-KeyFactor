from pathlib import Path

import pandas as pd
import pytest

from app.csv_loader import generate_process_quality, load_csv_path, load_frame, load_json_table, sample_csv_path, validate_target
from app.errors import ServiceError


def test_sample_file_types_columns(tmp_path: Path):
    path = tmp_path / "process-quality.csv"
    generate_process_quality(80).to_csv(path, index=False)
    df, columns = load_csv_path(path)
    names = [c.name for c in columns]
    types = {c.name: c.type for c in columns}
    assert "Temperature" in names
    assert "TargetConductivity" in names
    assert types["Temperature"] == "numeric"
    assert types["LineId"] == "categorical"
    assert types["PreheatMin"] == "numeric"
    assert len(df) == 80


def test_repo_sample_is_reman_sized():
    path = sample_csv_path()
    df, columns = load_csv_path(path)
    names = [c.name for c in columns]
    assert 190 <= len(df) <= 220
    assert names[0] == "Temperature"
    assert "TargetConductivity" in names
    process = [n for n in names if n != "TargetConductivity"]
    assert 6 <= len(process) <= 9


def test_rejects_empty_dataset():
    with pytest.raises(ServiceError) as exc:
        load_frame(pd.DataFrame())
    assert exc.value.error_code == "emptyDataset"


def test_rejects_missing_target():
    df, _ = load_json_table(["Temperature", "Quality"], [[1, 2], [3, 4]])
    with pytest.raises(ServiceError) as exc:
        validate_target(df, "Missing")
    assert exc.value.error_code == "missingTarget"


def test_rejects_empty_target():
    df, _ = load_json_table(["Temperature", "Quality"], [[1, None], [3, None]])
    with pytest.raises(ServiceError) as exc:
        validate_target(df, "Quality")
    assert exc.value.error_code == "emptyTarget"
