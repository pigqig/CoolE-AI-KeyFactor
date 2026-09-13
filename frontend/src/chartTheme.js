export function chartTokens() {
  const style = getComputedStyle(document.documentElement)
  const read = (name) => style.getPropertyValue(name).trim()
  return {
    ink: read('--ink') || '#2A333A',
    muted: read('--muted') || '#5A6770',
    line: read('--line') || '#B7C2CA',
    sheet: read('--sheet') || '#F3F6F8',
    mill: read('--mill') || '#D5DCE2',
    furnace: read('--furnace') || '#C26A16',
    meter: read('--meter') || '#0F6E7A',
    ok: read('--ok') || '#2F7A4B',
    bad: read('--bad') || '#A33B32',
    font: 'Barlow, "Noto Sans TC", "Microsoft JhengHei UI", sans-serif'
  }
}

export function valueAxis(tokens = chartTokens()) {
  return {
    axisLine: { lineStyle: { color: tokens.line } },
    axisTick: { lineStyle: { color: tokens.line } },
    axisLabel: { color: tokens.muted },
    splitLine: { lineStyle: { color: tokens.line, opacity: 0.55 } },
    nameTextStyle: { color: tokens.muted }
  }
}

export function categoryAxis(tokens = chartTokens()) {
  return {
    axisLine: { lineStyle: { color: tokens.line } },
    axisTick: { show: false },
    axisLabel: { color: tokens.ink },
    splitLine: { show: false },
    nameTextStyle: { color: tokens.muted }
  }
}

function mergeAxis(axis, tokens) {
  if (!axis) return axis
  const skin = {
    axisLine: { lineStyle: { color: tokens.line } },
    axisTick: { lineStyle: { color: tokens.line } },
    axisLabel: { color: tokens.muted },
    splitLine: { lineStyle: { color: tokens.line, opacity: 0.45 } },
    nameTextStyle: { color: tokens.muted }
  }
  if (Array.isArray(axis)) return axis.map((item) => mergeOneAxis(item, skin))
  return mergeOneAxis(axis, skin)
}

function mergeOneAxis(axis, skin) {
  return {
    ...skin,
    ...axis,
    axisLine: {
      ...skin.axisLine,
      ...(axis.axisLine || {}),
      lineStyle: { ...skin.axisLine.lineStyle, ...(axis.axisLine?.lineStyle || {}) }
    },
    axisTick: {
      ...skin.axisTick,
      ...(axis.axisTick || {}),
      lineStyle: { ...skin.axisTick.lineStyle, ...(axis.axisTick?.lineStyle || {}) }
    },
    axisLabel: { ...skin.axisLabel, ...(axis.axisLabel || {}) },
    splitLine: {
      ...skin.splitLine,
      ...(axis.splitLine || {}),
      lineStyle: { ...skin.splitLine.lineStyle, ...(axis.splitLine?.lineStyle || {}) }
    },
    nameTextStyle: { ...skin.nameTextStyle, ...(axis.nameTextStyle || {}) }
  }
}

export function applyChartTheme(option = {}) {
  const tokens = chartTokens()
  const title = option.title || {}
  return {
    ...option,
    backgroundColor: 'transparent',
    textStyle: { color: tokens.ink, fontFamily: tokens.font, ...(option.textStyle || {}) },
    title: {
      ...title,
      textStyle: { color: tokens.ink, fontFamily: tokens.font, fontSize: 13, ...(title.textStyle || {}) }
    },
    tooltip: {
      backgroundColor: tokens.sheet,
      borderColor: tokens.line,
      textStyle: { color: tokens.ink },
      ...(option.tooltip || {})
    },
    xAxis: mergeAxis(option.xAxis, tokens),
    yAxis: mergeAxis(option.yAxis, tokens)
  }
}
