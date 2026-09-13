/**
 * 建置前檢查 vue-i18n 訊息：字面 `{` `}` 若不是合法插值，編譯期會炸掉，
 * 且 production 的 drop_console 會把錯誤藏起來（管理頁就可能整片空白）。
 *
 * 本 repo 沒有授權貼上區（沒有 license.pasteHint）；這裡只攔「不該當插值的大括號」。
 * 合法寫法：{name}、{0}、{'literal'}；字面大括號請寫 {'{'} / {'}'}。
 */
import { readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const localesDir = fileURLToPath(new URL('../src/i18n/locales/', import.meta.url))

// vue-i18n 具名／列表插值：{name}、{0}
const NAMED_OR_LIST = /\{(?:[A-Za-z_][A-Za-z0-9_]*|\d+)\}/g
// 字面插值：{'...'}（含 {'{'} / {'}'}）
const LITERAL = /\{'(?:\\.|[^'\\])*'\}/g

function leftoverBraces(text) {
  const stripped = String(text).replace(LITERAL, '\0').replace(NAMED_OR_LIST, '\0')
  const hits = []
  for (let i = 0; i < stripped.length; i += 1) {
    const ch = stripped[i]
    if (ch === '{' || ch === '}') hits.push({ index: i, ch })
  }
  return hits
}

function walk(node, prefix, acc) {
  if (typeof node === 'string') {
    acc.push({ key: prefix, text: node })
    return
  }
  if (!node || typeof node !== 'object') return
  for (const [k, v] of Object.entries(node)) {
    walk(v, prefix ? `${prefix}.${k}` : k, acc)
  }
}

function scanMessages(locale, tree) {
  const rows = []
  walk(tree, '', rows)
  const errors = []
  for (const { key, text } of rows) {
    const hits = leftoverBraces(text)
    if (!hits.length) continue
    const preview = text.length > 120 ? `${text.slice(0, 117)}...` : text
    errors.push(
      `${locale} ${key}: 不合法的 vue-i18n 大括號（${hits.map((h) => h.ch).join(' ')}）。` +
        ` 字面 { } 請改成 {'{'} / {'}'}。內容：${preview}`
    )
  }
  return { count: rows.length, errors }
}

function assertDetector() {
  const bad = leftoverBraces('請貼上 { "lineId", "plantId" }')
  const good = leftoverBraces("已載入 {name}，第 {0} 筆，字面 {'{'}ok{'}'}")
  if (!bad.length) {
    throw new Error('check-i18n-messages: 偵測器沒抓到非法 { "lineId" } 範例')
  }
  if (good.length) {
    throw new Error('check-i18n-messages: 合法插值被誤判')
  }
}

async function tryCompileWithIntlify(locale, tree, errors) {
  let compile
  try {
    const mod = await import('@intlify/message-compiler')
    compile = mod.baseCompile || mod.compile
  } catch {
    return false
  }
  if (typeof compile !== 'function') return false

  const rows = []
  walk(tree, '', rows)
  for (const { key, text } of rows) {
    const compileErrors = []
    try {
      compile(text, {
        onError(err) {
          compileErrors.push(err?.message || String(err))
        }
      })
    } catch (err) {
      compileErrors.push(err?.message || String(err))
    }
    for (const msg of compileErrors) {
      errors.push(`${locale} ${key}: vue-i18n 編譯失敗：${msg}`)
    }
  }
  return true
}

assertDetector()

const files = readdirSync(localesDir).filter((f) => f.endsWith('.js'))
if (!files.length) {
  console.error('check-i18n-messages: 找不到語系檔', localesDir)
  process.exit(1)
}

const allErrors = []
let compiledWithIntlify = false
for (const file of files) {
  const locale = file.replace(/\.js$/, '')
  const mod = await import(pathToFileURL(join(localesDir, file)).href)
  const tree = mod.default
  if (!tree || typeof tree !== 'object') {
    allErrors.push(`${locale}: 語系檔沒有 default export 物件`)
    continue
  }
  const { count, errors } = scanMessages(locale, tree)
  allErrors.push(...errors)
  compiledWithIntlify = (await tryCompileWithIntlify(locale, tree, allErrors)) || compiledWithIntlify
  console.log(`check-i18n-messages: ${locale} ${count} 則字串`)
}

if (allErrors.length) {
  console.error(`check-i18n-messages: 失敗 ${allErrors.length} 項`)
  for (const line of allErrors) console.error('  -', line)
  process.exit(1)
}

console.log(
  compiledWithIntlify
    ? 'check-i18n-messages: OK（靜態掃描 + @intlify/message-compiler）'
    : 'check-i18n-messages: OK（靜態掃描）'
)
