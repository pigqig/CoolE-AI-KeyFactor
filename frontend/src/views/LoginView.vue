<script setup>
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { persistLocale } from '../i18n'
import { useSession } from '../stores/session'
import ThemeSwitch from '../components/ThemeSwitch.vue'

const { t, locale } = useI18n()
const session = useSession()
const username = ref('admin')
const password = ref('')
const busy = ref(false)

async function submit() {
  busy.value = true
  try {
    await session.login(username.value, password.value)
  } catch {
    /* session.errorCode */
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="login-wrap">
    <section class="card login-card">
      <h1>{{ t('app.product') }}</h1>
      <p class="lead">{{ t('login.lead') }}</p>
      <div class="login-tools">
        <div class="seg" :aria-label="t('locale.label')">
          <button type="button" :class="{ on: locale === 'zh-TW' }" @click="persistLocale('zh-TW')">{{ t('locale.zhTW') }}</button>
          <button type="button" :class="{ on: locale === 'en' }" @click="persistLocale('en')">{{ t('locale.en') }}</button>
          <button type="button" :class="{ on: locale === 'zh-CN' }" @click="persistLocale('zh-CN')">{{ t('locale.zhCN') }}</button>
        </div>
        <ThemeSwitch />
      </div>
      <form @submit.prevent="submit">
        <div class="field">
          <label for="kf-user">{{ t('login.username') }}</label>
          <input id="kf-user" v-model="username" autocomplete="username" />
        </div>
        <div class="field">
          <label for="kf-pass">{{ t('login.password') }}</label>
          <input id="kf-pass" v-model="password" type="password" autocomplete="current-password" />
        </div>
        <p v-if="session.errorCode" class="banner">{{ t('errors.' + session.errorCode) }}</p>
        <button type="submit" class="btn" :disabled="busy">{{ busy ? t('common.loading') : t('login.submit') }}</button>
      </form>
      <p class="hint">{{ t('login.seedHint') }}</p>
    </section>
  </div>
</template>
