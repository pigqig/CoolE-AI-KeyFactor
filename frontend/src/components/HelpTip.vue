<script setup>
const props = defineProps({ text: { type: String, required: true } })
const open = defineModel({ type: Boolean, default: false })
</script>

<template>
  <span class="tip-wrap">
    <button type="button" class="help" @click="open = !open">{{ $t('common.what') }}</button>
    <span v-if="open" class="tip">{{ props.text }}</span>
  </span>
</template>

<style scoped>
.tip-wrap { display: inline; }
.tip {
  display: block;
  margin-top: 6px;
  color: var(--muted);
  font-size: 0.88rem;
  line-height: 1.5;
  max-width: 68ch;
}
</style>
