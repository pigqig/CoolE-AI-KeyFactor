# CoolE KeyFactor

A **plant-floor bench** for remanufacturing. Drop a process CSV. It tells you which setting is actually driving quality, what happens if you change it, and whether QA signed off this model.

Built for QA, R&D, and process engineers — not for a notebook. UI defaults to Traditional Chinese.

[中文說明](README.md) · [Install](#install-in-five-minutes) · [Sponsor (JKOPay)](#sponsor)

![Key factors: temperature ~79.6%](docs/shot-importance.png)

Sample reman conductivity run: R² **0.825**, RMSE **0.945**. Temperature leads at about **79.6%**.

## What it fixes

When yield drops, the meeting guesses: more additive, longer dwell, tighter pressure. Nobody shares one ranked answer.

Analyst tools can compute this. They do not ship with plant accounts, draft→approve, or an audit trail. CoolE KeyFactor does.

- Plain-language cards first, charts second
- What-if before you touch the line (demo: conductivity 13.56 → 15.88)
- Engineer trains a Draft; QA / Admin approve the version the floor may use

## Install in five minutes

Need **.NET 8 SDK** and **Python 3.11+**. Node only if you change the UI. The repo already has a built frontend.

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

JKOPay / any TWQR app. Scan the code below, or enter JKO code **`3966`** (小言).

Same link as the GitHub Sponsor button: [pigqig#sponsor](https://github.com/pigqig#sponsor)

![JKOPay QR](docs/jkopay.png)

**Joseph_Li** · [pigqig@gmail.com](mailto:pigqig@gmail.com) · [ourcoolidea.com](http://ourcoolidea.com) · [MIT](LICENSE)

UI inspired by [explainerdashboard](https://github.com/oegedijk/explainerdashboard) (MIT).
