const KEY = 'kf-token'
const USER = 'kf-user'

export function readToken() {
  return sessionStorage.getItem(KEY) || ''
}

export function readUser() {
  try {
    return JSON.parse(sessionStorage.getItem(USER) || 'null')
  } catch {
    return null
  }
}

export function saveSession(token, user) {
  sessionStorage.setItem(KEY, token)
  sessionStorage.setItem(USER, JSON.stringify(user))
}

export function clearSession() {
  sessionStorage.removeItem(KEY)
  sessionStorage.removeItem(USER)
}

export function canMutate(role) {
  return role === 'Engineer' || role === 'Admin'
}

export function canApprove(role) {
  return role === 'Qa' || role === 'Admin'
}
