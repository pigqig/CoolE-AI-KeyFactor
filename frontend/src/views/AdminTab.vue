<script setup>
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '../api'
import EmptyState from '../components/EmptyState.vue'

const { t } = useI18n()
const users = ref([])
const audit = ref([])
const form = ref({ username: '', password: '', displayName: '', role: 'Viewer' })
const err = ref('')

onMounted(load)

async function load() {
  users.value = await api.listUsers()
  audit.value = await api.audit()
}

async function create() {
  err.value = ''
  try {
    await api.createUser(form.value)
    form.value = { username: '', password: '', displayName: '', role: 'Viewer' }
    await load()
  } catch (e) {
    err.value = e.errorCode || 'generic'
  }
}

async function disable(id) {
  await api.disableUser(id)
  await load()
}

async function role(id, next) {
  await api.assignRole(id, next)
  await load()
}
</script>

<template>
  <section class="card">
    <h2>{{ t('admin.users') }}</h2>
    <p class="lead">{{ t('admin.usersLead') }}</p>
    <p v-if="err" class="banner">{{ t('errors.' + err) }}</p>
    <div class="row-actions">
      <div class="field"><label>{{ t('login.username') }}</label><input v-model="form.username" /></div>
      <div class="field"><label>{{ t('login.password') }}</label><input v-model="form.password" type="password" /></div>
      <div class="field"><label>{{ t('admin.displayName') }}</label><input v-model="form.displayName" /></div>
      <div class="field">
        <label>{{ t('admin.role') }}</label>
        <select v-model="form.role">
          <option value="Admin">{{ t('roles.Admin') }}</option>
          <option value="Engineer">{{ t('roles.Engineer') }}</option>
          <option value="Qa">{{ t('roles.Qa') }}</option>
          <option value="Viewer">{{ t('roles.Viewer') }}</option>
        </select>
      </div>
      <button type="button" class="btn" @click="create">{{ t('admin.create') }}</button>
    </div>
    <EmptyState v-if="!users.length" :title="t('empty.adminUsers')" :body="t('empty.adminUsersBody')" />
    <div v-else class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>{{ t('login.username') }}</th>
            <th>{{ t('admin.displayName') }}</th>
            <th>{{ t('admin.role') }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in users" :key="u.id">
            <td>{{ u.username }}</td>
            <td>{{ u.displayName }}</td>
            <td>
              <select :value="u.role" @change="role(u.id, $event.target.value)">
                <option value="Admin">{{ t('roles.Admin') }}</option>
                <option value="Engineer">{{ t('roles.Engineer') }}</option>
                <option value="Qa">{{ t('roles.Qa') }}</option>
                <option value="Viewer">{{ t('roles.Viewer') }}</option>
              </select>
              <span v-if="!u.isActive" class="badge">{{ t('admin.disabled') }}</span>
            </td>
            <td>
              <button v-if="u.isActive" type="button" class="btn ghost" @click="disable(u.id)">{{ t('admin.disable') }}</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
  <section class="card">
    <h2>{{ t('admin.audit') }}</h2>
    <p class="lead">{{ t('admin.auditLead') }}</p>
    <EmptyState v-if="!audit.length" :title="t('empty.adminAudit')" :body="t('empty.adminAuditBody')" />
    <div v-else class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>{{ t('admin.at') }}</th>
            <th>{{ t('admin.action') }}</th>
            <th>{{ t('admin.entity') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="a in audit" :key="a.id">
            <td>{{ a.at }}</td>
            <td>{{ a.action }}</td>
            <td>{{ a.entityType }} {{ a.entityId }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
