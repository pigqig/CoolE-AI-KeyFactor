<script setup>
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { speakInsight } from '../i18n'
import { useShopFloor } from '../stores/shopFloor'
import { useSession } from '../stores/session'
import UnapprovedBanner from '../components/UnapprovedBanner.vue'
import ChartPanel from '../components/ChartPanel.vue'
import HelpTip from '../components/HelpTip.vue'
import EmptyState from '../components/EmptyState.vue'
import { chartTokens } from '../chartTheme'

const { t, te, locale } = useI18n()
const floor = useShopFloor()
const session = useSession()
const draft = ref({})
let timer

watch(
  () => floor.whatIf?.row,
  (row) => {
    draft.value = { ...(row || {}) }
  },
  { immediate: true }
)

function fname(name) {
  return te(`features.${name}`) ? t(`features.${name}`) : name
}

function schedule() {
  clearTimeout(timer)
  timer = setTimeout(() => {
    floor.whatIfSession.edits = { ...draft.value }
    floor.refreshWhatIf()
  }, 220)
}

function pick(i) {
  floor.whatIfSession = { rowIndex: Number(i), edits: {} }
  floor.refreshWhatIf()
}

function rand() {
  const n = floor.scoredRows?.rowCount || floor.dataset?.rowCount || 20
  pick(Math.floor(Math.random() * n))
}

const narrative = computed(() => speakInsight(t, te, floor.whatIf?.narrative || floor.whatIf?.insight))

const probaOpt = computed(() => {
  locale.value
  const tokens = chartTokens()
  const rows = floor.whatIf?.probabilities || []
  return {
    title: { text: t('charts.probaTitle') },
    xAxis: { type: 'value', name: t('charts.probaAxis'), max: 1 },
    yAxis: { type: 'category', data: rows.map((r) => r.label) },
    series: [{ type: 'bar', data: rows.map((r) => r.probability), itemStyle: { color: tokens.meter } }]
  }
})

const contribOpt = computed(() => {
  locale.value
  const tokens = chartTokens()
  const rows = [...(floor.whatIf?.contributions || [])].reverse()
  return {
    title: { text: t('charts.contribTitle') },
    xAxis: { type: 'value', name: t('charts.contribAxis') },
    yAxis: { type: 'category', data: rows.map((r) => fname(r.feature)) },
    series: [
      {
        type: 'bar',
        data: rows.map((r) => ({
          value: r.delta,
          itemStyle: { color: r.delta >= 0 ? tokens.ok : tokens.bad }
        }))
      }
    ]
  }
})
</script>

<template>
  <section class="card">
    <h2>{{ t('whatif.heading') }}</h2>
    <p class="lead">
      {{ t('whatif.lead') }}
      <HelpTip :text="t('glossary.whatif')" />
    </p>
    <EmptyState v-if="!floor.hasModel" :title="t('empty.needModel')" :body="t('empty.needModelBody')" />
    <UnapprovedBanner v-if="floor.hasModel" />
    <p v-if="floor.hasModel && !session.mayTrain" class="lead">{{ t('auth.readOnlyHint') }}</p>
    <div v-if="floor.hasModel && session.mayTrain" class="grid-2">
      <div>
        <div class="row-actions">
          <div class="field">
            <label>{{ t('whatif.pickRow') }}</label>
            <input :value="floor.whatIfSession.rowIndex" type="number" min="0" :disabled="!session.mayTrain" @change="pick($event.target.value)" />
          </div>
          <button type="button" class="btn blue" :disabled="!session.mayTrain" @click="rand">{{ t('whatif.random') }}</button>
        </div>
        <EmptyState v-if="!floor.whatIf" :title="t('empty.whatifNoRow')" :body="t('empty.whatifNoRowBody')" />
        <div v-for="col in floor.featureCols" :key="col.name" class="field">
          <label>{{ fname(col.name) }}</label>
          <select v-if="col.type === 'categorical'" v-model="draft[col.name]" @change="schedule">
            <option :value="draft[col.name]">{{ draft[col.name] }}</option>
          </select>
          <input v-else v-model.number="draft[col.name]" type="number" step="any" @input="schedule" />
        </div>
      </div>
      <div>
        <div class="metrics">
          <div v-if="floor.whatIf?.predictedClass" class="metric">
            <span>{{ t('whatif.predictedClass') }}</span>
            <b>{{ floor.whatIf.predictedClass }}</b>
          </div>
          <div class="metric"><span>{{ t('whatif.baseline') }}</span><b>{{ floor.whatIf?.baselineClass || floor.whatIf?.baseline?.toFixed?.(3) || '—' }}</b></div>
          <div class="metric"><span>{{ t('whatif.current') }}</span><b>{{ floor.whatIf?.prediction?.toFixed?.(3) ?? '—' }}</b></div>
          <div v-if="!floor.whatIf?.predictedClass" class="metric"><span>{{ t('whatif.mean') }}</span><b>{{ floor.whatIf?.meanPrediction?.toFixed?.(3) ?? '—' }}</b></div>
        </div>
        <p class="insight">{{ narrative }}</p>
        <ChartPanel v-if="floor.whatIf?.probabilities?.length" :option="probaOpt" class="short" />
        <p class="lead">{{ t('whatif.contrib') }}</p>
        <ChartPanel :option="contribOpt" />
      </div>
    </div>
  </section>
</template>
