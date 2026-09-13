<script setup>
defineProps({
  title: { type: String, required: true },
  body: { type: String, default: '' }
})
</script>

<template>
  <div class="empty-state" role="status">
    <p class="empty-title">{{ title }}</p>
    <p v-if="body" class="empty-body">{{ body }}</p>
    <slot />
  </div>
</template>
