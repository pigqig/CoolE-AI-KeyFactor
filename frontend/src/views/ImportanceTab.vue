<script setup>
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { speakInsight } from '../i18n'
import { useShopFloor } from '../stores/shopFloor'
import UnapprovedBanner from '../components/UnapprovedBanner.vue'
import ChartPanel from '../components/ChartPanel.vue'
import HelpTip from '../components/HelpTip.vue'
import EmptyState from '../components/EmptyState.vue'
import { chartTokens } from '../chartTheme'

const { t, te, locale } = useI18n()
const floor = useShopFloor()

function fname(name) {
  return te(`features.${name}`) ? t(`features.${name}`) : name
}

const cards = computed(() => floor.factorRank?.insights || [])
const option = computed(() => {
  locale.value
  const tokens = chartTokens()
  const rows = [...(floor.factorRank?.importances || [])].reverse()
  return {
    title: { text: t('charts.importanceTitle'), left: 8 },
    tooltip: { trigger: 'axis' },
    grid: { left: 120, right: 24, top: 40, bottom: 32 },
    xAxis: { type: 'value', name: t('charts.importanceAxis') },
    yAxis: { type: 'category', data: rows.map((r) => fname(r.feature)) },
    series: [{ type: 'bar', data: rows.map((r) => r.importance), itemStyle: { color: tokens.meter } }]
  }
})

function extra(card) {
  if (card.direction === 'up') return t('insights.directionUp', { name: fname(card.feature), target: floor.targetColumn })
  if (card.direction === 'down') return t('insights.directionDown', { name: fname(card.feature), target: floor.targetColumn })
  return ''
}

watch(
  () => floor.topN,
  () => {
    if (floor.hasModel) floor.refreshFactors()
  }
)
</script>

<template>
  <section class="card">
    <h2>{{ t('importance.heading') }}</h2>
    <p class="lead">
      {{ t('importance.lead') }}
      <HelpTip :text="t('glossary.importance')" />
    </p>
    <EmptyState v-if="!floor.hasModel" :title="t('empty.needModel')" :body="t('empty.needModelBody')" />
    <template v-else>
      <UnapprovedBanner />
      <p class="lead">{{ t('importance.method') }} <HelpTip :text="t('glossary.shap')" /></p>
      <div class="field">
        <label>{{ t('importance.topN') }}</label>
        <input v-model.number="floor.topN" type="range" min="3" max="12" />
      </div>
      <ChartPanel :option="option" tall />
      <article v-for="card in cards" :key="card.feature" class="factor-card">
        <div class="meta">{{ t('importance.rank', { n: card.rank }) }}　{{ t('importance.share', { n: ((card.share || 0) * 100).toFixed(1) }) }}</div>
        <strong>{{ fname(card.feature) }}</strong>
        <p>{{ speakInsight(t, te, card) }} {{ extra(card) }}</p>
      </article>
    </template>
  </section>
</template>
