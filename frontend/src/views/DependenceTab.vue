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

watch(
  () => [floor.depFeature, floor.colorBy, floor.depClass],
  () => {
    if (floor.hasModel && floor.depFeature) floor.refreshDependence()
  }
)

const classOptions = computed(() => floor.model?.classes || floor.model?.metrics?.classes || [])

const leftOpt = computed(() => {
  locale.value
  const tokens = chartTokens()
  const rows = [...(floor.factorRank?.importances || [])].reverse()
  return {
    grid: { left: 100, right: 12, top: 16, bottom: 24 },
    xAxis: { type: 'value' },
    yAxis: { type: 'category', data: rows.map((r) => fname(r.feature)) },
    series: [{ type: 'bar', data: rows.map((r) => r.importance), itemStyle: { color: tokens.meter } }]
  }
})

const rightOpt = computed(() => {
  locale.value
  const tokens = chartTokens()
  const dep = floor.dependence
  if (!dep) return {}
  const cats = dep.pdp.some((p) => p.xLabel)
  return {
    title: { text: t('charts.pdpTitle') },
    tooltip: { trigger: 'axis' },
    xAxis: {
      type: cats ? 'category' : 'value',
      name: fname(dep.feature),
      data: cats ? dep.pdp.map((p) => p.xLabel) : undefined
    },
    yAxis: { type: 'value', name: t('charts.pdpY') },
    series: [
      {
        type: 'scatter',
        data: (dep.points || []).map((p) => (cats ? [p.xLabel, p.y] : [p.x, p.y])),
        symbolSize: 6,
        itemStyle: { color: tokens.meter, opacity: 0.35 }
      },
      {
        type: 'line',
        data: dep.pdp.map((p) => (cats ? [p.xLabel, p.y] : [p.x, p.y])),
        itemStyle: { color: tokens.furnace }
      }
    ]
  }
})

const caption = computed(() => speakInsight(t, te, floor.dependence?.insight))
</script>

<template>
  <section class="card">
    <h2>{{ t('dependence.heading') }}</h2>
    <p class="lead">
      {{ t('dependence.lead') }}
      <HelpTip :text="t('glossary.pdp')" />
    </p>
    <EmptyState v-if="!floor.hasModel" :title="t('empty.needModel')" :body="t('empty.needModelBody')" />
    <UnapprovedBanner v-if="floor.hasModel" />
    <div v-if="floor.hasModel" class="grid-2">
      <div>
        <ChartPanel :option="leftOpt" />
      </div>
      <div>
        <div class="row-actions">
          <div class="field">
            <label>{{ t('dependence.pick') }}</label>
            <select v-model="floor.depFeature">
              <option v-for="col in floor.featureCols" :key="col.name" :value="col.name">{{ fname(col.name) }}</option>
            </select>
          </div>
          <div class="field">
            <label>{{ t('dependence.colorBy') }}</label>
            <select v-model="floor.colorBy">
              <option value="">{{ t('dependence.none') }}</option>
              <option v-for="col in floor.featureCols" :key="col.name" :value="col.name">{{ fname(col.name) }}</option>
            </select>
          </div>
          <div v-if="classOptions.length" class="field">
            <label>{{ t('dependence.class') }}</label>
            <select v-model="floor.depClass">
              <option value="">{{ t('dependence.classDefault') }}</option>
              <option v-for="lab in classOptions" :key="lab" :value="lab">{{ lab }}</option>
            </select>
          </div>
        </div>
        <ChartPanel :option="rightOpt" />
        <p class="insight">{{ caption }}</p>
      </div>
    </div>
  </section>
</template>
