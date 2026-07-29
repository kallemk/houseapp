import { apiClient } from './client'
import type {
  ContractorInfoDto,
  CostType,
  ProjectDto,
  ProjectPriority,
  ProjectStatus,
  WorkType,
} from './types'

export interface ProjectCostInput {
  type: CostType
  description: string | null
  amount: number
  dateIncurred: string
  isBudgeted: boolean
}

export interface ProjectMilestoneInput {
  description: string
  plannedDate: string | null
  completedDate: string | null
}

/**
 * One shape for create and update: a project is a single Cosmos document, written whole — costs and
 * contractor included. There are deliberately no sub-resource endpoints for them.
 */
export interface SaveProjectInput {
  name: string
  description: string | null
  notes: string | null
  workType: WorkType
  componentId: string
  status: ProjectStatus
  priority: ProjectPriority
  isUrgent: boolean
  plannedStartDate: string | null
  actualStartDate: string | null
  completedDate: string | null
  estimatedDurationDays: number | null
  estimatedCost: number
  estimatedValueIncrease: number | null
  expectedLifespanYears: number | null
  energyEfficiencyGainPercent: number | null
  contractor: ContractorInfoDto | null
  costs: ProjectCostInput[]
  milestones: ProjectMilestoneInput[]
}

export const projectsApi = {
  listForProperty: (propertyId: string) => apiClient.get<ProjectDto[]>(`/properties/${propertyId}/projects`),
  getById: (id: string, propertyId: string) => apiClient.get<ProjectDto>(`/projects/${id}?propertyId=${propertyId}`),
  create: (propertyId: string, input: SaveProjectInput) =>
    apiClient.post<ProjectDto>(`/properties/${propertyId}/projects`, input),
  update: (id: string, propertyId: string, input: SaveProjectInput) =>
    apiClient.put<void>(`/projects/${id}?propertyId=${propertyId}`, input),
  remove: (id: string, propertyId: string) => apiClient.delete<void>(`/projects/${id}?propertyId=${propertyId}`),
}
