<script setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { speakInsight } from '../i18n'

const props = defineProps({
  insight: { type: Object, default: null },
  extra: { type: String, default: '' }
})

const { t, te } = useI18n()
const text = computed(() => {
  const main = speakInsight(t, te, props.insight)
  return [main, props.extra].filter(Boolean).join(' ')
})
</script>

<template>
  <p v-if="text" class="insight">{{ text }}</p>
</template>
