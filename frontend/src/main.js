import '@fontsource/barlow/400.css'
import '@fontsource/barlow/600.css'
import '@fontsource/barlow-condensed/600.css'
import '@fontsource/barlow-condensed/700.css'
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { i18n, syncHtmlLang } from './i18n'
import { bootTheme } from './theme'
import './style.css'

bootTheme()
syncHtmlLang(i18n.global.locale.value)
const app = createApp(App)
app.use(createPinia())
app.use(i18n)
app.mount('#app')
