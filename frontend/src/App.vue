<script setup>
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { persistLocale } from './i18n'
import { useShopFloor } from './stores/shopFloor'
import { useSession } from './stores/session'
import DataTab from './views/DataTab.vue'
import ModelTab from './views/ModelTab.vue'
import ImportanceTab from './views/ImportanceTab.vue'
import DependenceTab from './views/DependenceTab.vue'
import WhatIfTab from './views/WhatIfTab.vue'
import PredictionsTab from './views/PredictionsTab.vue'
import AdminTab from './views/AdminTab.vue'
import LoginView from './views/LoginView.vue'
import ThemeSwitch from './components/ThemeSwitch.vue'

const { t, locale } = useI18n()
const floor = useShopFloor()
const session = useSession()

const tabs = computed(() => {
  const list = [
    { id: 'data', key: 'tabs.data' },
    { id: 'model', key: 'tabs.model' },
    { id: 'importance', key: 'tabs.importance' },
    { id: 'dependence', key: 'tabs.dependence' },
    { id: 'whatif', key: 'tabs.whatif' },
    { id: 'predictions', key: 'tabs.predictions' }
  ]
  if (session.isAdmin) list.push({ id: 'admin', key: 'tabs.admin' })
  return list
})

const err = computed(() => (floor.errorCode ? t(`errors.${floor.errorCode}`) : ''))

const datasetChip = computed(() => {
  if (!floor.dataset) return { title: t('statusBar.data'), detail: t('statusBar.noData') }
  return {
    title: t('statusBar.data'),
    detail: t('statusBar.dataset', { name: floor.dataset.name, n: floor.dataset.rowCount })
  }
})

const modelChip = computed(() => {
  if (!floor.model) return { title: t('statusBar.model'), detail: t('statusBar.noModel') }
  const status = floor.model.status ? t('status.' + floor.model.status) : t('status.Draft')
  return {
    title: t('statusBar.model'),
    detail: t('statusBar.modelValue', { n: floor.model.versionNumber ?? '—', status })
  }
})

watch(
  () => floor.tab,
  (tab) => {
    if (tab === 'dependence' && floor.hasModel) floor.refreshDependence()
    if (tab === 'whatif' && floor.hasModel && session.mayTrain) floor.refreshWhatIf()
    if (tab === 'predictions' && floor.hasModel) floor.refreshRows()
  }
)

function dumpJson() {
  const blob = new Blob([JSON.stringify(floor.snapshot(), null, 2)], { type: 'application/json' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = 'key-factor-analysis.json'
  a.click()
}

onMounted(() => session.restore())
</script>

<template>
  <div v-if="!session.ready" class="boot-blank"></div>
  <LoginView v-else-if="!session.signedIn" />
  <div v-else class="shell">
    <header class="station-bar">
      <div class="brand">
        <h1>{{ t('app.product') }}</h1>
        <p>{{ t('app.tagline') }}</p>
      </div>
      <div class="status-well">
        <div class="status-chip">
          <span>{{ datasetChip.title }}</span>
          <b>{{ datasetChip.detail }}</b>
        </div>
        <div class="status-chip">
          <span>{{ modelChip.title }}</span>
          <b>{{ modelChip.detail }}</b>
        </div>
        <div v-if="floor.busy" class="status-chip busy-flag">{{ t('common.loading') }}</div>
      </div>
      <div class="top-actions">
        <div class="who">
          <strong>{{ session.user.displayName }}</strong>
          <span>{{ t('roles.' + session.user.role) }} {{ session.user.username }}</span>
        </div>
        <div class="seg on-steel" :aria-label="t('locale.label')">
          <button type="button" :class="{ on: locale === 'zh-TW' }" @click="persistLocale('zh-TW')">{{ t('locale.zhTW') }}</button>
          <button type="button" :class="{ on: locale === 'en' }" @click="persistLocale('en')">{{ t('locale.en') }}</button>
          <button type="button" :class="{ on: locale === 'zh-CN' }" @click="persistLocale('zh-CN')">{{ t('locale.zhCN') }}</button>
        </div>
        <ThemeSwitch surface="steel" />
        <a class="btn ghost" href="/swagger" target="_blank" rel="noreferrer">{{ t('app.swagger') }}</a>
        <button type="button" class="btn ghost" :title="t('app.downloadHint')" @click="dumpJson">{{ t('app.download') }}</button>
        <button type="button" class="btn ghost" @click="session.logout()">{{ t('auth.logout') }}</button>
      </div>
    </header>

    <div class="workspace">
      <nav class="rail" :aria-label="t('tabs.nav')">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          :class="{ on: floor.tab === tab.id }"
          :aria-current="floor.tab === tab.id ? 'page' : undefined"
          @click="floor.tab = tab.id"
        >
          {{ t(tab.key) }}
        </button>
      </nav>

      <main class="bench">
        <div v-if="err" class="banner">{{ t('common.error') }}：{{ err }}</div>
        <p v-else-if="!session.mayTrain && floor.tab !== 'admin'" class="notice">{{ t('auth.readOnlyHint') }}</p>
        <DataTab v-if="floor.tab === 'data'" />
        <ModelTab v-else-if="floor.tab === 'model'" />
        <ImportanceTab v-else-if="floor.tab === 'importance'" />
        <DependenceTab v-else-if="floor.tab === 'dependence'" />
        <WhatIfTab v-else-if="floor.tab === 'whatif'" />
        <PredictionsTab v-else-if="floor.tab === 'predictions'" />
        <AdminTab v-else-if="floor.tab === 'admin' && session.isAdmin" />
      </main>
    </div>
  </div>
</template>
