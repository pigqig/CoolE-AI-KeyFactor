# CoolE KeyFactor

**廠務檢驗台。** 再製造現場丟一份製程 CSV，它告訴你：品質是被哪個參數帶走的、調多少會變好、這版模型品保有沒有簽過。

不是給資料科學家用的筆記本，是給**品保、研發、製程工程**上線用的檯面。繁體中文預設，可切 English / 简体中文。

[安裝](#五分鐘裝起來) · [它解決什麼](#它解決什麼) · [街口贊助](#贊助) · [English](README.en.md)

![關鍵因子：溫度約佔 79.6%，先對齊溫度再談別的](docs/shot-importance.png)

範例再製導電率：R² **0.825**、RMSE **0.945**。第一名因子是溫度（約 **79.6%**），不是添加劑。

---

## 它解決什麼

產線上溫度、壓力、時間、添加劑、濕度同時在跑。良率掉的時候，會議上各說各話：有人要加添加劑，有人要延長持壓。沒有一份大家認的「先調這個」。

分析師工具（ExplainerDashboard、Shapash）能算，但進不了廠：沒有帳號、沒有核准、沒有稽核、畫面不是給值班的人看的。

CoolE KeyFactor 把同一件事做成檢驗台：

1. **先講人話** — 「溫度大約占 80%，再製請先對齊。」圖表只是佐證。
2. **先試再改線** — What-if 拉參數，看導電率從 13.56 變成 15.88，再決定要不要動 SOP。
3. **品保簽過才能當標準** — 工程師訓練是草稿，品保／管理員核准後才是現場用的版本。誰訓的、誰簽的，稽核留著。

適用：再製造、導電膠／電極、塗佈、烘烤、組裝後品質。目標可以是連續值（導電率），也可以是 OK／NG、返工／報廢、A／B／C。

---

## 現場一天怎麼用

1. **資料** — 上傳現場 CSV，或一鍵載入再製範例／OK／NG 範例。
2. **模型** — 選自動、迴歸、二元或多類。分類會出混淆矩陣與每類精確率。
3. **關鍵因子** — 卡片先告訴你該盯哪一個。
4. **因子依存** — 只改一個參數，品質怎麼走。
5. **What-if** — 改條件看預測差；點一筆紀錄當起點。

![What-if：導電率 13.56 → 15.88](docs/shot-whatif.png)

![OK／NG 分類與混淆矩陣](docs/shot-okng.png)

廠內角色現成就位：Admin、工程、品保、唯讀。Viewer 只看、不改線。

---

## 五分鐘裝起來

需要 **.NET 8 SDK** 與 **Python 3.11+**。改畫面才需要 Node 20+。這個 repo 已帶編譯好的前端，clone 後可以直接跑。

```bash
git clone https://github.com/pigqig/CoolE-AI-KeyFactor.git
cd CoolE-AI-KeyFactor

python3 -m venv python/.venv
source python/.venv/bin/activate          # Windows: python\.venv\Scripts\activate
pip install -r python/requirements.txt

dotnet run --project src/KeyFactorDashboard
```

瀏覽器開 http://127.0.0.1:43123

| | |
| --- | --- |
| 第一次登入 | `admin` / `ChangeMe!123`（請立刻改密，或到「管理」開工程／品保帳號） |
| 想立刻看到結果 | 登入後按「載入範例資料」，再訓練 |
| 要練 OK／NG | 按「載入 OK/NG 範例」 |

進階（改畫面、SQL Server、機台 API）寫在 [docs/USAGE.md](docs/USAGE.md)。

---

## 贊助

如果 KeyFactor 幫你少開一次無效實驗、或品保少吵一場，歡迎用街口請一杯。

1. 打開街口支付，或任何支援 TWQR 的銀行／電支 App
2. 掃描下方收款碼
3. 或在街口支付輸入街口代碼 **`3966`**（小言）

也可從 GitHub 右上 Sponsor 按鈕連到同一頁：[pigqig#sponsor](https://github.com/pigqig#sponsor)

![街口支付收款碼 · 小言 · 3966](docs/jkopay.png)

合作、授權、客製：<pigqig@gmail.com>（主旨寫 KeyFactor）

---

## 作者

**Joseph_Li** · 酷意 AI  
郵件：[pigqig@gmail.com](mailto:pigqig@gmail.com)  
網站：[ourcoolidea.com](http://ourcoolidea.com)

[MIT](LICENSE) © 2026 Joseph_Li

畫面靈感來自 [explainerdashboard](https://github.com/oegedijk/explainerdashboard)（MIT）。
