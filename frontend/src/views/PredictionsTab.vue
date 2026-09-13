<script setup>
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useShopFloor } from '../stores/shopFloor'
import UnapprovedBanner from '../components/UnapprovedBanner.vue'
import EmptyState from '../components/EmptyState.vue'

const { t } = useI18n()
const floor = useShopFloor()
onMounted(() => {
  if (floor.hasModel) floor.refreshRows()
})
</script>

<template>
  <section class="card">
    <h2>{{ t('predictions.heading') }}</h2>
    <p class="lead">{{ t('predictions.lead') }}</p>
    <EmptyState v-if="!floor.hasModel" :title="t('empty.needModel')" :body="t('empty.needModelBody')" />
    <UnapprovedBanner v-if="floor.hasModel" />
    <EmptyState
      v-else-if="!(floor.scoredRows?.rows || []).length"
      :title="t('empty.predictions')"
      :body="t('empty.predictionsBody')"
    />
    <div v-else class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>{{ t('predictions.index') }}</th>
            <th>{{ t('predictions.actual') }}</th>
            <th>{{ t('predictions.predicted') }}</th>
            <th>{{ t('predictions.residual') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in floor.scoredRows?.rows || []"
            :key="row.index"
            class="clickable"
            @click="floor.jumpWhatIf(row.index)"
          >
            <td>{{ row.index }}</td>
            <td>{{ row.actual }}</td>
            <td>{{ typeof row.predicted === 'number' ? row.predicted.toFixed(3) : row.predicted }}</td>
            <td>{{ row.residual == null ? '' : row.residual.toFixed(3) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
