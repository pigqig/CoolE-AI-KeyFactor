<script setup>
import { useI18n } from 'vue-i18n'
import { useShopFloor } from '../stores/shopFloor'

const { t } = useI18n()
const floor = useShopFloor()
</script>

<template>
  <p v-if="floor.unapproved" class="notice">{{ t('model.unapprovedBanner') }}</p>
</template>
