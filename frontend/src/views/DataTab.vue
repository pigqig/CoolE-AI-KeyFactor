<script setup>
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { speakInsight } from '../i18n'
import { api } from '../api'
import { useShopFloor } from '../stores/shopFloor'
import { useSession } from '../stores/session'
import EmptyState from '../components/EmptyState.vue'
const { t, te } = useI18n()
const floor = useShopFloor()
const session = useSession()
const fileEl = ref(null)
const history = ref([])

onMounted(loadHistory)

async function loadHistory() {
  try {
    history.value = await api.listDatasets()
  } catch {
    history.value = []
  }
}

function featureName(col) {
  const key = `features.${col}`
  return te(key) ? t(key) : col
}

const overview = computed(() => {
  const ov = floor.dataset?.overview
  if (!ov) return ''
  const args = { ...(ov.args || {}), target: floor.targetColumn || ov.targetColumn || '' }
  const key = floor.targetColumn ? 'insights.datasetOverviewWithTarget' : `insights.${ov.insightCode}`
  return te(key) ? t(key, args) : speakInsight(t, te, ov)
})

async function onFile(ev) {
  const file = ev.target.files?.[0]
  if (!file) return
  await floor.ingestFile(file)
  await loadHistory()
}

async function sample() {
  await floor.ingestSample()
  await loadHistory()
}

async function sampleOkng() {
  await floor.ingestSampleOkng()
  await loadHistory()
}

async function openOld(id) {
  await floor.openDataset(id)
}
</script>

<template>
  <section class="card">
    <h2>{{ t('data.heading') }}</h2>
    <p class="lead">{{ t('data.lead') }}</p>
    <div v-if="session.mayTrain" class="row-actions">
      <button type="button" class="btn" @click="fileEl?.click()">{{ t('data.upload') }}</button>
      <input ref="fileEl" type="file" accept=".csv,text/csv" hidden @change="onFile" />
      <button type="button" class="btn blue" :disabled="floor.busy" @click="sample">{{ t('data.sample') }}</button>
      <button type="button" class="btn blue" :disabled="floor.busy" @click="sampleOkng">{{ t('data.sampleOkng') }}</button>
    </div>
    <p v-else class="lead">{{ t('auth.readOnlyHint') }}</p>
    <p class="lead">{{ t('data.fileHint') }}</p>

    <EmptyState v-if="!floor.dataset" :title="t('empty.dataTitle')" :body="t('empty.dataBody')" />
    <template v-else>
      <p class="insight">{{ overview }}</p>
      <div class="field">
        <label>{{ t('data.pickTarget') }}</label>
        <select v-model="floor.targetColumn">
          <option v-for="col in floor.dataset.columns" :key="col.name" :value="col.name">
            {{ featureName(col.name) }} ({{ col.name }})
          </option>
        </select>
      </div>
      <h3>{{ t('data.columns') }}</h3>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>{{ t('common.feature') }}</th>
              <th></th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="col in floor.dataset.columns" :key="col.name">
              <td>{{ featureName(col.name) }} <span class="badge">{{ col.name }}</span></td>
              <td>{{ col.type === 'numeric' ? t('common.numeric') : t('common.categorical') }}</td>
              <td v-if="col.missingCount">{{ t('common.missing', { n: col.missingCount }) }}</td>
              <td v-else></td>
            </tr>
          </tbody>
        </table>
      </div>
      <h3>{{ t('data.preview') }}</h3>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th v-for="col in floor.dataset.columns" :key="col.name">{{ col.name }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, i) in floor.dataset.previewRows" :key="i">
              <td v-for="col in floor.dataset.columns" :key="col.name">{{ row[col.name] }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </section>

  <section v-if="history.length" class="card">
    <h2>{{ t('data.history') }}</h2>
    <table>
      <tbody>
        <tr v-for="item in history" :key="item.id" class="clickable" @click="openOld(item.id)">
          <td>{{ item.name }}</td>
          <td>{{ item.rowCount }}</td>
          <td>{{ item.id.slice(0, 8) }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
