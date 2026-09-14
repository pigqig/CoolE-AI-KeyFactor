# CoolE KeyFactor

A **plant-floor QC dashboard** for remanufacturing and process quality. Upload CSV process data; the bench trains a **scikit-learn (sklearn)** **Gradient Boosting** or **Random Forest** model, ranks **key factors** with **permutation importance** (feature importance), and plots **partial dependence** for one parameter at a time. Run a what-if before changing the line. A model version is usable as a floor standard only after QA approval.

Built for **QA, R&D, and process engineering** — an operator-facing console, not an analyst notebook. Free edition: login and roles, train, key factors, dependence, what-if, predictions, admin. The UI defaults to Traditional Chinese.

**Stack:** ASP.NET Core (C#), Vue 3, ECharts, EF Core, SQLite / SQL Server, and Python (scikit-learn). C# hosts the API; Vue and ECharts render the bench; EF Core uses SQLite by default and can switch to SQL Server. Explanations use sklearn `permutation_importance` and `partial_dependence` — SHAP is not required.

[中文說明](README.md) · [Install](#install-in-five-minutes) · [Sponsor (JKOPay)](#sponsor)

![Key factors: temperature accounts for about 79.6%](docs/shot-importance.png)

Sample remanufacturing conductivity run: coefficient of determination R² **0.825**, RMSE **0.945**. Temperature is the leading factor (about **79.6%**); additive is not the main driver.

## What it solves

Temperature, pressure, time, additive, and humidity move together. When yield drops, the meeting splits: add more additive, or extend dwell. There is no shared order of which parameter to adjust first.

Tools such as ExplainerDashboard and Shapash can compute permutation importance and partial dependence, but they are hard to put on the floor: no plant accounts, no model-approval flow, no audit trail, and a UI that is not meant for shift staff.

CoolE KeyFactor turns the same machine-learning explanation into a bench:

- Results are written in plain language first; charts are there to verify
- Run a what-if before changing the line (demo: predicted conductivity from 13.56 to 15.88)
- An engineer’s training is saved as a draft. After QA or an administrator approves it, that version becomes the one the floor may cite. Training, approval, and rejection are written to the audit log, so you can see who submitted the model and who approved it

Quality targets can be continuous (conductivity) or categorical (OK/NG, rework/scrap, A/B/C). Typical lines: remanufacturing, conductive paste / electrodes, coating, bake, post-assembly quality, and other process-quality work.

## A day on the floor

1. **Data** — upload a plant CSV, or load the remanufacturing / OK-NG sample.
2. **Model** — choose auto, regression, binary, or multiclass (sklearn Gradient Boosting or Random Forest). Classification shows a confusion matrix and per-class precision.
3. **Key factors** — permutation importance ranks feature importance; plain-language notes say which parameter to control first and roughly how much it contributes.
4. **Dependence** — sklearn partial dependence: hold other settings, vary one parameter, watch the quality prediction.
5. **What-if** — compare predictions before and after an edit; you can start from a single plant record.
6. **Predictions** — actual vs predicted per row; click a row to open What-if.

Roles are built in: administrator, engineer, QA, and read-only. A read-only account can view results, not train or submit a simulation. Admin can create users, change roles, and read the audit log.

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
