export interface MeResponse {
  id: string
  email: string
  displayName: string
  /** Admins additionally manage users and property components. The API enforces this — the UI only hides. */
  isAdmin: boolean
}

export interface UserDto {
  id: string
  email: string
  displayName: string
  hasPassword: boolean
  isAdmin: boolean
  createdAt: string
}

export type PropertyType = 'House' | 'Apartment' | 'Townhouse' | 'Cottage' | 'Other'

export interface PropertyDto {
  id: string
  nickname: string
  address: string
  /** All three are null on properties created before these fields existed. */
  address2: string | null
  yearBuilt: number | null
  type: PropertyType | null
  purchaseDate: string
  purchasePrice: number
  createdAt: string
}

export type DocumentCategory = 'Deed' | 'Warranty' | 'Receipt' | 'Photo' | 'Other'

/** A part of the house a project can concern — admin-managed data, not a fixed list. */
export interface PropertyComponentDto {
  id: string
  name: string
  recommendedIntervalMonths: number | null
}

export interface ValuationEntryDto {
  id: string
  propertyId: string
  date: string
  value: number
  source: string | null
  notes: string | null
  createdByUserId: string
  createdAt: string
}

export type WorkType = 'Maintenance' | 'Renovation' | 'Investment'
export type ProjectStatus = 'Planned' | 'InProgress' | 'Completed' | 'OnHold' | 'Cancelled'
export type ProjectPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type CostType = 'Materials' | 'Labor' | 'Tools' | 'Permits' | 'Other'

export interface ProjectCostDto {
  id: string
  type: CostType
  description: string | null
  amount: number
  dateIncurred: string
  isBudgeted: boolean
}

export interface ContractorInfoDto {
  name: string
  phone: string | null
  email: string | null
  website: string | null
  quotedPrice: number | null
  quotedDate: string | null
  notes: string | null
}

export interface ProjectDto {
  id: string
  propertyId: string
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
  /** Derived server-side from the cost rows — never sent on write. */
  actualCost: number
  estimatedValueIncrease: number | null
  expectedLifespanYears: number | null
  energyEfficiencyGainPercent: number | null
  contractor: ContractorInfoDto | null
  costs: ProjectCostDto[]
  createdByUserId: string
  createdAt: string
}

export interface DocumentDto {
  id: string
  propertyId: string
  projectId: string | null
  date: string
  fileName: string
  contentType: string
  sizeBytes: number
  category: DocumentCategory
  uploadedByUserId: string
  uploadedAt: string
}
