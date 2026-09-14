import { defineStore } from 'pinia'
import { api } from '../api'

export const useShopFloor = defineStore('shopFloor', {
  state: () => ({
    tab: 'data',
    busy: false,
    errorCode: '',
    errorDetail: '',
    dataset: null,
    targetColumn: '',
    algorithm: 'gbr',
    task: 'auto',
    model: null,
    factorRank: null,
    topN: 8,
    depFeature: '',
    colorBy: '',
    depClass: '',
    dependence: null,
    whatIf: null,
    whatIfSession: { rowIndex: 0, edits: {} },
    scoredRows: null,
    versions: []
  }),
  getters: {
    featureCols: (s) =>
      (s.dataset?.columns || []).filter((c) => c.name !== s.targetColumn),
    hasModel: (s) => Boolean(s.model?.id),
    unapproved: (s) => Boolean(s.model) && s.model.status !== 'Approved'
  },
  actions: {
    clearError() {
      this.errorCode = ''
      this.errorDetail = ''
    },
    async wrap(fn) {
      this.busy = true
      this.errorCode = ''
      this.errorDetail = ''
      try {
        return await fn()
      } catch (err) {
        this.errorCode = err.errorCode || 'generic'
        this.errorDetail = err.message || ''
        throw err
      } finally {
        this.busy = false
      }
    },
    async ingestFile(file) {
      await this.wrap(async () => {
        this.resetModel()
        this.dataset = await api.uploadCsv(file)
        this.guessTarget()
        await this.refreshVersions()
      })
    },
    async ingestSample() {
      await this.wrap(async () => {
        this.resetModel()
        this.dataset = await api.loadSample()
        this.guessTarget()
        await this.refreshVersions()
      })
    },
    async ingestSampleOkng() {
      await this.wrap(async () => {
        this.resetModel()
        this.dataset = await api.loadSampleOkng()
        this.guessTarget()
        this.task = 'auto'
        await this.refreshVersions()
      })
    },
    guessTarget() {
      const cols = this.dataset?.columns || []
      const names = cols.map((c) => c.name)
      if (names.includes('QualityResult')) {
        this.targetColumn = 'QualityResult'
        return
      }
      if (names.includes('TargetConductivity')) {
        this.targetColumn = 'TargetConductivity'
        return
      }
      const numeric = cols.filter((c) => c.type === 'numeric' && !String(c.name).startsWith('Unnamed'))
      const hinted = numeric.find((c) => /導電|conductivity|quality|lpc|yield|良率/i.test(c.name))
      this.targetColumn = hinted?.name || numeric[numeric.length - 1]?.name || ''
    },
    resetModel() {
      this.model = null
      this.factorRank = null
      this.dependence = null
      this.whatIf = null
      this.scoredRows = null
      this.whatIfSession = { rowIndex: 0, edits: {} }
      this.versions = []
    },
    pickPreferred(list) {
      return list.find((v) => v.status === 'Approved')
        || list.find((v) => v.status === 'Draft')
        || list[0]
        || null
    },
    async refreshVersions() {
      if (!this.dataset?.id) return
      this.versions = await api.versions(this.dataset.id)
      if (this.model) {
        const still = this.versions.find((v) => v.id === this.model.id)
        if (still) {
          this.model = { ...this.model, ...still }
          return
        }
      }
      if (this.versions.length) {
        this.model = this.pickPreferred(this.versions)
      }
    },
    async approveNow(id, note) {
      await this.wrap(async () => {
        this.model = await api.approve(id, note)
        await this.refreshVersions()
        await this.refreshFactors()
      })
    },
    async rejectNow(id, note) {
      await this.wrap(async () => {
        this.model = await api.reject(id, note)
        await this.refreshVersions()
      })
    },
    async openDataset(id) {
      await this.wrap(async () => {
        this.resetModel()
        this.dataset = await api.getDataset(id, this.targetColumn)
        this.guessTarget()
        await this.refreshVersions()
        if (this.model) await this.refreshFactors()
      })
    },
    async selectVersion(id) {
      this.model = this.versions.find((v) => v.id === id) || (await api.getModel(id))
      await this.refreshFactors()
    },
    async trainNow() {
      if (!this.dataset?.id || !this.targetColumn) return
      await this.wrap(async () => {
        this.model = await api.train(this.dataset.id, this.targetColumn, this.algorithm, this.task)
        await this.refreshVersions()
        await this.refreshFactors()
        this.depFeature = this.factorRank?.importances?.[0]?.feature || this.featureCols[0]?.name || ''
        this.tab = 'importance'
      })
    },
    async refreshFactors() {
      if (!this.model?.id) return
      this.factorRank = await api.importances(this.model.id, this.topN)
    },
    async refreshDependence() {
      if (!this.model?.id || !this.depFeature) return
      await this.wrap(async () => {
        this.dependence = await api.dependence(
          this.model.id,
          this.depFeature,
          this.colorBy || undefined,
          this.depClass || undefined
        )
      })
    },
    async refreshWhatIf() {
      if (!this.model?.id) return
      await this.wrap(async () => {
        this.whatIf = await api.whatif(this.model.id, {
          rowIndex: this.whatIfSession.rowIndex,
          edits: this.whatIfSession.edits
        })
      })
    },
    async refreshRows() {
      if (!this.model?.id) return
      await this.wrap(async () => {
        this.scoredRows = await api.rows(this.model.id)
      })
    },
    jumpWhatIf(index) {
      this.whatIfSession = { rowIndex: index, edits: {} }
      this.tab = 'whatif'
      return this.refreshWhatIf()
    },
    snapshot() {
      return {
        dataset: this.dataset,
        model: this.model,
        factorRank: this.factorRank,
        dependence: this.dependence
      }
    }
  }
})
