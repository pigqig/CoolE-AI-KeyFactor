# 發行／混淆

Repo 裡的 C# / Vue / Python 是給人改的。`publish/` 與 `wwwroot` 裡的發行檔才做壓縮／混淆。

## Vue

```bash
cd frontend
npm ci
npm run build
```

`npm run build` 會先跑 `check-i18n-messages`：語系字串裡不合法的 `{...}`（vue-i18n 會當插值編譯，例如 `{ "lineId", ... }`）會讓建置失敗，避免 production `drop_console` 把錯誤藏起來、管理頁整片空白。字面大括號請寫 `{'{'}` / `{'}'}`。通過後才 Vite 建置，再用 `javascript-obfuscator` 處理 `wwwroot/assets/*.js`（保留 Vue / ECharts 名稱）。

## C#

```bash
dotnet publish src/KeyFactorDashboard -c Release -o publish
```

`tools/obfuscar.xml` 給 [Obfuscar](https://github.com/obfuscar/obfuscar) 用。對 ASP.NET 入口與 Controller 名稱手下留情，否則路由會爛。本機若沒裝 Obfuscar console，略過即可，網站仍可跑。

```bash
# 有裝 obfuscar.console 時
cd publish
obfuscar.console ../tools/obfuscar.xml
```

## Python

不要 PyArmor。sklearn 服務維持 `pip install -r python/requirements.txt` 能跑。要編譯 `.pyc` 自己決定，原始 `.py` 留在 repo。
