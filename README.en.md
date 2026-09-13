# CoolE KeyFactor

A **plant-floor bench** for remanufacturing. Upload a process record (CSV). The system ranks which process parameters most affect quality, shows how the prediction changes if you adjust them, and whether this model version has been approved by QA for use on the floor.

Built for **QA, R&D, and process engineering** — an operator-facing console, not an analyst notebook. The UI defaults to Traditional Chinese.

**Stack:** ASP.NET Core (C#), Vue 3, ECharts, EF Core, SQLite / SQL Server, and Python (scikit-learn). C# hosts the API; Vue and ECharts render the bench; EF Core uses SQLite by default and can switch to SQL Server.

[中文說明](README.md) · [Install](#install-in-five-minutes) · [Sponsor (JKOPay)](#sponsor)

![Key factors: temperature accounts for about 79.6%](docs/shot-importance.png)

Sample remanufacturing conductivity run: coefficient of determination R² **0.825**, RMSE **0.945**. Temperature is the leading factor (about **79.6%**); additive is not the main driver.

## What it solves

Temperature, pressure, time, additive, and humidity move together. When yield drops, the meeting splits: add more additive, or extend dwell. There is no shared order of which parameter to adjust first.

Tools such as ExplainerDashboard and Shapash can compute this, but they are hard to put on the floor: no plant accounts, no model-approval flow, no audit trail, and a UI that is not meant for shift staff.

CoolE KeyFactor turns the same analysis into a bench:

- Results are written in plain language first; charts are there to verify
- Run a what-if before changing the line (demo: predicted conductivity from 13.56 to 15.88)
- An engineer’s training is saved as a draft. After QA or an administrator approves it, that version becomes the one the floor may cite. Training, approval, and rejection are written to the audit log, so you can see who submitted the model and who approved it.

Quality targets can be continuous (conductivity) or categorical (OK/NG, rework/scrap, A/B/C).

## A day on the floor

1. **Data** — upload a plant CSV, or load the remanufacturing / OK-NG sample.
2. **Model** — choose auto, regression, binary, or multiclass. Classification shows a confusion matrix and per-class precision.
3. **Key factors** — cards state which parameter to control first, and roughly how much it contributes.
4. **Dependence** — hold other settings, vary one parameter, watch the quality prediction.
5. **What-if** — compare predictions before and after an edit; you can start from a single plant record.

Roles are built in: administrator, engineer, QA, and read-only. A read-only account can view results, not train or submit a simulation.

## Install in five minutes

Need **.NET 8 SDK** and **Python 3.11+**. Node is required only if you change the UI. The repo already includes a built frontend.

```bash
git clone https://github.com/pigqig/CoolE-AI-KeyFactor.git
cd CoolE-AI-KeyFactor
python3 -m venv python/.venv
source python/.venv/bin/activate
pip install -r python/requirements.txt
dotnet run --project src/KeyFactorDashboard
```

Open http://127.0.0.1:43123 — sign in with `admin` / `ChangeMe!123`, then load the sample.

Roles, SQL Server, and machine API: [docs/USAGE.md](docs/USAGE.md).

## Sponsor

If this tool helps your process analysis or QA workflow, you can support further development with JKOPay.

Open JKOPay or any TWQR app, scan the code below, or enter JKO code **`3966`** (小言).

Same page as the GitHub Sponsor button: [pigqig#sponsor](https://github.com/pigqig#sponsor)

![JKOPay QR](docs/jkopay.png)

**Joseph_Li** · [pigqig@gmail.com](mailto:pigqig@gmail.com) · [ourcoolidea.com](http://ourcoolidea.com) · [MIT](LICENSE)

UI inspired by [explainerdashboard](https://github.com/oegedijk/explainerdashboard) (MIT).
