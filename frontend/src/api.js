import { clearSession, readToken } from './auth'

const jsonHeaders = { Accept: 'application/json' }

function headers(extra = {}) {
  const h = { ...jsonHeaders, ...extra }
  const token = readToken()
  if (token) h.Authorization = `Bearer ${token}`
  return h
}

async function read(res) {
  const text = await res.text()
  let body = null
  try {
    body = text ? JSON.parse(text) : null
  } catch {
    body = { message: text }
  }
  if (res.status === 401) {
    clearSession()
  }
  if (!res.ok) {
    const err = new Error(body?.detail || body?.message || res.statusText)
    err.status = res.status
    err.errorCode = body?.errorCode || body?.title || (res.status === 403 ? 'forbidden' : 'generic')
    throw err
  }
  return body
}

export const api = {
  health: () => fetch('/api/v1/health', { headers: jsonHeaders }).then(read),
  login: (username, password) =>
    fetch('/api/v1/auth/login', {
      method: 'POST',
      headers: { ...jsonHeaders, 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    }).then(read),
  logout: () => fetch('/api/v1/auth/logout', { method: 'POST', headers: headers() }).then(read).catch(() => ({})),
  me: () => fetch('/api/v1/auth/me', { headers: headers() }).then(read),
  listUsers: () => fetch('/api/v1/users', { headers: headers() }).then(read),
  createUser: (body) =>
    fetch('/api/v1/users', {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(body)
    }).then(read),
  disableUser: (id) => fetch(`/api/v1/users/${id}/disable`, { method: 'POST', headers: headers() }).then(read),
  assignRole: (id, role) =>
    fetch(`/api/v1/users/${id}/role`, {
      method: 'PUT',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ role })
    }).then(read),
  audit: () => fetch('/api/v1/audit', { headers: headers() }).then(read),
  uploadCsv(file) {
    const fd = new FormData()
    fd.append('file', file)
    return fetch('/api/v1/datasets', { method: 'POST', headers: headers(), body: fd }).then(read)
  },
  loadSample: () => fetch('/api/v1/datasets/sample', { method: 'POST', headers: headers() }).then(read),
  loadSampleOkng: () => fetch('/api/v1/datasets/sample-okng', { method: 'POST', headers: headers() }).then(read),
  listDatasets: () => fetch('/api/v1/datasets', { headers: headers() }).then(read),
  getDataset: (id, target) => {
    const q = target ? `?target=${encodeURIComponent(target)}` : ''
    return fetch(`/api/v1/datasets/${id}${q}`, { headers: headers() }).then(read)
  },
  versions: (datasetId) => fetch(`/api/v1/datasets/${datasetId}/versions`, { headers: headers() }).then(read),
  train: (datasetId, targetColumn, algorithm, task) =>
    fetch('/api/v1/models/train', {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ datasetId, targetColumn, algorithm, task })
    }).then(read),
  getModel: (id) => fetch(`/api/v1/models/${id}`, { headers: headers() }).then(read),
  approve: (id, note) =>
    fetch(`/api/v1/models/${id}/approve`, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ note })
    }).then(read),
  reject: (id, note) =>
    fetch(`/api/v1/models/${id}/reject`, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ note })
    }).then(read),
  importances: (id, topN) => {
    const q = topN ? `?topN=${topN}` : ''
    return fetch(`/api/v1/models/${id}/importances${q}`, { headers: headers() }).then(read)
  },
  dependence: (id, feature, colorBy, className) => {
    const q = new URLSearchParams({ feature })
    if (colorBy) q.set('colorBy', colorBy)
    if (className) q.set('class', className)
    return fetch(`/api/v1/models/${id}/dependence?${q}`, { headers: headers() }).then(read)
  },
  whatif: (id, payload) =>
    fetch(`/api/v1/models/${id}/whatif`, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(payload)
    }).then(read),
  rows: (id) => fetch(`/api/v1/models/${id}/rows`, { headers: headers() }).then(read)
}
