<script setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import * as echarts from 'echarts'
import { applyChartTheme } from '../chartTheme'

const props = defineProps({
  option: { type: Object, default: () => ({}) },
  tall: { type: Boolean, default: false }
})

const el = ref(null)
let chart

function paint() {
  if (!chart) return
  chart.setOption(applyChartTheme(props.option || {}), true)
}

onMounted(() => {
  chart = echarts.init(el.value)
  paint()
  window.addEventListener('resize', resize)
  window.addEventListener('kf-theme-change', paint)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  window.removeEventListener('kf-theme-change', paint)
  chart?.dispose()
})

watch(() => props.option, paint, { deep: true })

function resize() {
  chart?.resize()
}
</script>

<template>
  <div ref="el" class="chart" :class="{ short: !tall }" />
</template>
