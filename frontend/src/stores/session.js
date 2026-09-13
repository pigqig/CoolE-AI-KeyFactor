import { defineStore } from 'pinia'
import { api } from '../api'
import { canApprove, canMutate, clearSession, readToken, readUser, saveSession } from '../auth'

export const useSession = defineStore('session', {
  state: () => ({
    token: readToken(),
    user: readUser(),
    errorCode: '',
    ready: false
  }),
  getters: {
    signedIn: (s) => Boolean(s.token && s.user),
    role: (s) => s.user?.role || '',
    mayTrain: (s) => canMutate(s.user?.role),
    mayApprove: (s) => canApprove(s.user?.role),
    isAdmin: (s) => s.user?.role === 'Admin'
  },
  actions: {
    async login(username, password) {
      this.errorCode = ''
      try {
        const res = await api.login(username, password)
        this.token = res.token
        this.user = res.user
        this.ready = true
        saveSession(res.token, res.user)
      } catch (err) {
        this.errorCode = err.errorCode || 'badCredentials'
        throw err
      }
    },
    async restore() {
      if (this.token) {
        try {
          this.user = await api.me()
          saveSession(this.token, this.user)
        } catch {
          this.token = ''
          this.user = null
          clearSession()
        }
      }
      this.ready = true
    },
    async logout() {
      try {
        await api.logout()
      } catch {
        /* token already dead */
      }
      this.token = ''
      this.user = null
      this.ready = true
      clearSession()
    }
  }
})
