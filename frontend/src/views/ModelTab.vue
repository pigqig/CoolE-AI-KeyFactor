<script setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { speakInsight } from '../i18n'
import { useShopFloor } from '../stores/shopFloor'
import { useSession } from '../stores/session'
import InsightBlock from '../components/InsightBlock.vue'
import HelpTip from '../components/HelpTip.vue'
import UnapprovedBanner from '../components/UnapprovedBanner.vue'
import EmptyState from '../components/EmptyState.vue'

const { t, te } = useI18n()
const floor = useShopFloor()
const session = useSession()
const m = computed(() => floor.model?.metrics)
const confusion = computed(() => m.value?.confusion)
const classCards = computed(() => floor.factorRank?.classInsights || [])
const isCls = computed(() => floor.model?.task === 'classification')

function speakCard(card) {
  return speakInsight(t, te, card)
}

function versionMetric(v) {
  if (v.metrics?.accuracy != null) return Number(v.metrics.accuracy).toFixed(3)
  if (v.metrics?.rSquared != null) return Number(v.metrics.rSquared).toFixed(3)
  return '—'
}

async function approve(id) {
  await floor.approveNow(id, window.prompt(t('model.notePrompt')) || '')
}

async function reject(id) {
  await floor.rejectNow(id, window.prompt(t('model.notePrompt')) || '')
}
</script>

<template>
  <section class="card">
    <h2>{{ t('model.heading') }}</h2>
    <p class="lead">{{ t('model.lead') }}</p>
    <EmptyState v-if="!floor.dataset" :title="t('empty.modelNeedData')" :body="t('empty.modelNeedDataBody')" />
    <template v-else>
      <UnapprovedBanner />
      <div class="row-actions">
        <div class="field">
          <label>{{ t('model.algorithm') }}</label>
          <select v-model="floor.algorithm" :disabled="!session.mayTrain">
            <option value="gbr">{{ t('model.gbr') }}</option>
            <option value="rf">{{ t('model.rf') }}</option>
          </select>
        </div>
        <div class="field">
          <label>{{ t('model.task') }}</label>
          <select v-model="floor.task" :disabled="!session.mayTrain">
            <option value="auto">{{ t('model.taskAuto') }}</option>
            <option value="regression">{{ t('model.taskRegPick') }}</option>
            <option value="binary">{{ t('model.taskBinary') }}</option>
            <option value="multiclass">{{ t('model.taskMulti') }}</option>
          </select>
        </div>
        <button
          v-if="session.mayTrain"
          type="button"
          class="btn"
          :disabled="floor.busy || !floor.targetColumn"
          @click="floor.trainNow()"
        >
          {{ floor.busy ? t('model.training') : t('model.train') }}
        </button>
      </div>
      <p v-if="!session.mayTrain" class="lead">{{ t('auth.readOnlyHint') }}</p>

      <template v-if="floor.versions.length">
        <h3>{{ t('model.versions') }}</h3>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>{{ t('model.colVersion') }}</th>
                <th>{{ t('model.colStatus') }}</th>
                <th>{{ t('model.colAlgo') }}</th>
                <th>{{ t('model.colScore') }}</th>
                <th>{{ t('model.colBy') }}</th>
                <th>{{ t('common.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="v in floor.versions" :key="v.id" :class="{ on: floor.model?.id === v.id }">
                <td>v{{ v.versionNumber }}</td>
                <td><span class="chip" :class="v.status">{{ t('status.' + v.status) }}</span></td>
                <td>{{ v.algorithm }}</td>
                <td>{{ versionMetric(v) }}</td>
                <td>{{ v.trainedBy || '—' }}</td>
                <td class="row-actions">
                  <button type="button" class="btn ghost" @click="floor.selectVersion(v.id)">{{ t('model.use') }}</button>
                  <button
                    v-if="session.mayApprove && v.status === 'Draft'"
                    type="button"
                    class="btn ok"
                    @click="approve(v.id)"
                  >{{ t('model.approve') }}</button>
                  <button
                    v-if="session.mayApprove && v.status === 'Draft'"
                    type="button"
                    class="btn ghost"
                    @click="reject(v.id)"
                  >{{ t('model.reject') }}</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>

      <EmptyState v-if="!floor.model" :title="t('empty.modelEmpty')" :body="t('empty.modelEmptyBody')" />
      <template v-else>
        <p class="insight">{{ t('model.done') }}</p>
        <p>
          {{ floor.model.task === 'classification' ? t('model.taskCls') : t('model.taskReg') }}
          <HelpTip :text="t('glossary.importance')" />
        </p>
        <h3>{{ t('model.metrics') }}</h3>
        <div class="metrics">
          <div v-if="m?.rSquared != null" class="metric"><span>{{ t('model.r2') }}</span><b>{{ m.rSquared.toFixed(3) }}</b></div>
          <div v-if="m?.rmse != null" class="metric"><span>{{ t('model.rmse') }}</span><b>{{ m.rmse.toFixed(3) }}</b></div>
          <div v-if="m?.mae != null" class="metric"><span>{{ t('model.mae') }}</span><b>{{ m.mae.toFixed(3) }}</b></div>
          <div v-if="m?.accuracy != null" class="metric"><span>{{ t('model.accuracy') }}</span><b>{{ (m.accuracy * 100).toFixed(1) }}%</b></div>
          <div v-if="m?.auc != null" class="metric"><span>{{ t('model.auc') }}</span><b>{{ m.auc.toFixed(3) }}</b></div>
          <div v-if="m?.f1 != null" class="metric"><span>{{ t('model.f1') }}</span><b>{{ m.f1.toFixed(3) }}</b></div>
        </div>
        <InsightBlock v-if="floor.model.overview" :insight="floor.model.overview" />
        <template v-if="isCls && confusion?.matrix">
          <h3>{{ t('model.confusion') }}</h3>
          <p class="lead">{{ t('model.confusionHint') }}</p>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th></th>
                  <th v-for="lab in confusion.labels" :key="lab">{{ t('model.predOf', { label: lab }) }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, i) in confusion.matrix" :key="i">
                  <th>{{ t('model.actualOf', { label: confusion.labels[i] }) }}</th>
                  <td v-for="(cell, j) in row" :key="j" :class="{ hit: i === j }">{{ cell }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <article v-for="(card, i) in classCards" :key="i" class="factor-card">
            <p>{{ speakCard(card) }}</p>
          </article>
        </template>
      </template>
    </template>
  </section>
</template>
