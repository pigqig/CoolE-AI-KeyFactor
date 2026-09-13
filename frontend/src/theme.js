export const THEME_KEY = 'kf-theme'
export const THEME_PREFS = ['light', 'dark', 'system']

export function readPref() {
  try {
    const value = localStorage.getItem(THEME_KEY)
    return THEME_PREFS.includes(value) ? value : 'light'
  } catch {
    return 'light'
  }
}

export function resolveTheme(pref = readPref()) {
  if (pref === 'system') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  }
  return pref === 'dark' ? 'dark' : 'light'
}

export function applyTheme(pref) {
  const next = THEME_PREFS.includes(pref) ? pref : 'light'
  try {
    localStorage.setItem(THEME_KEY, next)
  } catch {
    /* private mode */
  }
  const resolved = resolveTheme(next)
  const root = document.documentElement
  root.setAttribute('data-theme-pref', next)
  root.setAttribute('data-theme', resolved)
  root.style.colorScheme = resolved
  window.dispatchEvent(new CustomEvent('kf-theme-change', { detail: { pref: next, resolved } }))
  return { pref: next, resolved }
}

export function bootTheme() {
  applyTheme(readPref())
  const media = window.matchMedia('(prefers-color-scheme: dark)')
  const onSystem = () => {
    if (readPref() === 'system') applyTheme('system')
  }
  if (media.addEventListener) media.addEventListener('change', onSystem)
  else media.addListener(onSystem)
}
