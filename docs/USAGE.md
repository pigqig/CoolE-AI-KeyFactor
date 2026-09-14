# 操作備註

把 CSV 製程資料交給 sklearn（Gradient Boosting / Random Forest）訓練後，在廠務 QC 儀表板看關鍵因子（permutation importance）、因子依存（partial dependence）、What-if 與單筆預測。畫面走同一套 `/api/v1`，Swagger 也是。未登入會先看到登入頁。

## 第一次登入

種子帳號（雜湊入庫，明文只出現在文件）：

- 帳號：`admin`
- 密碼：`ChangeMe!123`

登入後請到「管理」另開工程師／品保帳號，或至少改掉預設密碼（目前未強制改密）。Token 存在瀏覽器 `sessionStorage`，畫面要求加 `Authorization: Bearer`。

## 角色

| 角色 | 能做什麼 |
| --- | --- |
| Admin | 使用者、核准／退回模型、訓練與進件 |
| Engineer | 訓練、What-if、進件、提出 Draft 版本 |
| Qa | 唯讀儀表板，核准或退回版本 |
| Viewer | 只看儀表板與 GET API |

## 畫面步驟

1. 開 [http://127.0.0.1:43123](http://127.0.0.1:43123)，用廠內帳號登入。
2. **資料**：上傳 CSV 或「載入範例資料」。目標欄預設 `TargetConductivity`。Viewer／Qa 只能打開既有資料集。
3. **模型**：工程師選任務後訓練，得到 Draft。
   - **自動**：目標剛好兩個相異值 → 二元分類，否則迴歸。
   - **迴歸／二元／多類**：強制該任務；二元不是 2 類、多類少於 3 類會 400。
   - 分類會顯示混淆矩陣（列＝實際、欄＝模型判的）與每類筆數／精確率／召回率。
   - 品保／管理員可核准或退回。同一資料集只會有一個 Approved；再核准會把舊的標成 Retired。沒有核准版時，關鍵因子／What-if 用最新 Draft，並顯示「未核准，僅供試驗」。
   - OK/NG 範例：`POST /api/v1/datasets/sample-okng` 或 `POST /api/v1/datasets/sample?kind=okng`。
4. **關鍵因子**：卡片先講人話，長條圖只是佐證。
5. **因子依存**：掃一個參數，看預測導電率怎麼走。
6. **What-if**／**單筆預測**：工程師改條件、看差值。Viewer 不能送 What-if。
7. **管理**（僅 Admin）：使用者列表／新增／停用／改角色，以及最近 200 筆稽核。

語言在右上角，存在 `localStorage`（`kf-locale`）。

## API 授權

- 登入：`POST /api/v1/auth/login` `{ "username", "password" }` → `{ token, user }`
- 之後每個 `/api/v1`（健康檢查除外）加 `Authorization: Bearer <token>`
- 登出：`POST /api/v1/auth/logout`（前端丟 token）；`GET /api/v1/auth/me`
- 機台進件可改用 `X-Api-Key`（設定 `Api:Key`）。正確金鑰視為 Engineer；送錯金鑰才 401；沒送金鑰則改看 JWT
- Viewer 可 GET datasets／models／importances／dependence／rows
- Engineer 與 Admin 可 POST train／whatif／ingest
- Admin 與 Qa 可 POST approve／reject
- `GET /api/v1/audit` 僅 Admin

```bash
TOKEN=$(curl -s -X POST http://127.0.0.1:43123/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"ChangeMe!123"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["token"])')

curl -s -X POST http://127.0.0.1:43123/api/v1/datasets/json \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"line-A","columns":["Temperature","Pressure","TargetConductivity"],"rows":[[180,2.1,12.4],[176,2.3,11.8]]}'
```
