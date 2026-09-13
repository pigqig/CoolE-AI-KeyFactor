import { createI18n } from 'vue-i18n'
import zhTW from './locales/zh-TW'
import zhCN from './locales/zh-CN'
import en from './locales/en'

const KEY = 'kf-locale'
const saved = typeof localStorage !== 'undefined' ? localStorage.getItem(KEY) : null
const locale = saved === 'en' || saved === 'zh-CN' || saved === 'zh-TW' ? saved : 'zh-TW'

export const i18n = createI18n({
  legacy: false,
  locale,
  fallbackLocale: 'zh-TW',
  messages: {
    'zh-TW': zhTW,
    'zh-CN': zhCN,
    en
  }
})

export function persistLocale(next) {
  i18n.global.locale.value = next
  localStorage.setItem(KEY, next)
  syncHtmlLang(next)
}

export function syncHtmlLang(code) {
  document.documentElement.lang = code
}

export function speakInsight(t, te, insight) {
  if (!insight) return ''
  const code = insight.insightCode
  const key = code ? `insights.${code}` : ''
  const args = flatten(insight.args)
  if (key && te(key)) return t(key, args)
  return insight.summary || ''
}

function flatten(args) {
  if (!args || typeof args !== 'object') return {}
  const out = {}
  for (const [k, v] of Object.entries(args)) {
    if (v && typeof v === 'object' && 'valueKind' in v) continue
    out[k] = v
  }
  return out
}
