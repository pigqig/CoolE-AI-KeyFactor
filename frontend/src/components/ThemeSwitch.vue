<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { applyTheme, readPref, resolveTheme } from '../theme'

defineProps({
  surface: { type: String, default: 'sheet' }
})

const { t } = useI18n()
const pref = ref(readPref())

function choose(next) {
  applyTheme(next)
  pref.value = next
}

function sync(event) {
  pref.value = event.detail?.pref || readPref()
}

onMounted(() => {
  pref.value = readPref()
  applyTheme(pref.value)
  window.addEventListener('kf-theme-change', sync)
})

onBeforeUnmount(() => {
  window.removeEventListener('kf-theme-change', sync)
})

const options = [
  { id: 'light', key: 'theme.light' },
  { id: 'dark', key: 'theme.dark' },
  { id: 'system', key: 'theme.system' }
]

void resolveTheme
</script>

<template>
  <div class="seg" :class="'on-' + surface" :aria-label="t('theme.label')" role="group">
    <button
      v-for="opt in options"
      :key="opt.id"
      type="button"
      :class="{ on: pref === opt.id }"
      :aria-pressed="pref === opt.id"
      @click="choose(opt.id)"
    >
      {{ t(opt.key) }}
    </button>
  </div>
</template>
