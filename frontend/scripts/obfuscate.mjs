import { readdirSync, readFileSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'
import { fileURLToPath } from 'node:url'
import JavaScriptObfuscator from 'javascript-obfuscator'

const root = fileURLToPath(new URL('../../src/KeyFactorDashboard/wwwroot/', import.meta.url))
const assets = join(root, 'assets')

function walk(dir, acc = []) {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name)
    if (statSync(p).isDirectory()) walk(p, acc)
    else acc.push(p)
  }
  return acc
}

if (!existsSync(assets)) {
  console.warn('obfuscate: no assets dir, skip')
  process.exit(0)
}

const files = walk(assets).filter((f) => extname(f) === '.js')
for (const file of files) {
  const src = readFileSync(file, 'utf8')
  const out = JavaScriptObfuscator.obfuscate(src, {
    compact: true,
    controlFlowFlattening: false,
    deadCodeInjection: false,
    stringArray: true,
    stringArrayEncoding: ['base64'],
    stringArrayThreshold: 0.4,
    renameGlobals: false,
    reservedNames: [
      '^Vue',
      '^vue',
      '^echarts',
      '^Pinia',
      '^pinia',
      '^i18n'
    ],
    sourceMap: false,
    target: 'browser'
  }).getObfuscatedCode()
  writeFileSync(file, out)
  console.log('obfuscated', file)
}
