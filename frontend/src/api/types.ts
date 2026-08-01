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
  /** All of these are null on properties created before the field existed. */
  address2: string | null
  postalCode: string | null
  city: string | null
  country: string | null
  /** Fastighetsbeteckning. */
  propertyDesignation: string | null
  yearBuilt: number | null
  type: PropertyType | null
  /** WGS84. Both null means no map is shown. */
  latitude: number | null
  longitude: number | null
  purchaseDate: string
  purchasePrice: number
  /** The shared sandbox — every signed-in user can see and edit it. */
  isDemo: boolean
  /** False when you can only see this because it's the demo. Gates sharing and deletion. */
  isMember: boolean
  /** Where this property's documents go. Blob unless someone has connected Google Drive. */
  documentStorage: DocumentStorageKind
  /** Link to the Drive folder. Null unless connected. */
  driveFolderUrl: string | null
  /** Whose Drive grant every upload uses — so a broken connection can name who must renew it. */
  driveConnectedByName: string | null
  createdAt: string
}

export interface PropertyMemberDto {
  userId: string
  email: string
  displayName: string
}

export type DocumentCategory = 'Deed' | 'Warranty' | 'Receipt' | 'Photo' | 'Other' | 'Invoice'

/** A part of the house a project can concern — admin-managed data, not a fixed list. */
export interface PropertyComponentDto {
  id: string
  name: string
  recommendedIntervalMonths: number | null
}

/** How a property's component compares to the central registry — computed on read, never stored. */
export type ComponentOrigin = 'Central' | 'Modified' | 'Local'

export interface PropertyLocalComponentDto {
  id: string
  name: string
  recommendedIntervalMonths: number | null
  origin: ComponentOrigin
  /** What central says, so a Modified row can show what it differs from. Null unless Modified. */
  centralName: string | null
  centralIntervalMonths: number | null
}

export interface PropertyComponentSetDto {
  /** False while the property still follows the central registry. */
  customized: boolean
  components: PropertyLocalComponentDto[]
  /** Central components this property doesn't have, which a sync would add. */
  availableFromCentralCount: number
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

export type WorkType = 'Maintenance' | 'Renovation' | 'Investment' | 'Purchase'
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

export interface ProjectMilestoneDto {
  id: string
  description: string
  plannedDate: string | null
  completedDate: string | null
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
  /** True for work too minor to reset the component's maintenance clock. */
  excludeFromMaintenanceSchedule: boolean
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
  milestones: ProjectMilestoneDto[]
  createdByUserId: string
  createdAt: string
}

export type MaintenanceUrgency = 'NotScheduled' | 'Unknown' | 'Ok' | 'DueSoon' | 'Overdue'

/** Where lastCompletedDate came from — YearBuilt means "assumed, correct it by logging real work". */
export type MaintenanceBaseline = 'None' | 'Project' | 'YearBuilt'

/** Entirely derived server-side from component intervals and completed maintenance projects. */
export interface MaintenanceScheduleItemDto {
  componentId: string
  componentName: string
  recommendedIntervalMonths: number | null
  lastCompletedDate: string | null
  baseline: MaintenanceBaseline
  lastProjectId: string | null
  lastProjectName: string | null
  nextDueDate: string | null
  monthsUntilDue: number | null
  urgency: MaintenanceUrgency
  hasUpcomingProject: boolean
}

export interface BudgetLineDto {
  workType: WorkType
  budgeted: number
  spent: number
  remaining: number
}

export interface BudgetDto {
  id: string | null
  propertyId: string
  year: number
  lines: BudgetLineDto[]
  totalBudgeted: number
  totalSpent: number
}

export interface DocumentDto {
  id: string
  propertyId: string
  projectId: string | null
  date: string
  /** Null on documents uploaded before titles existed — fall back to fileName. */
  title: string | null
  fileName: string
  contentType: string
  sizeBytes: number
  category: DocumentCategory
  storageKind: DocumentStorageKind
  /** Drive's own link. Null on Blob documents, which go through a short-lived SAS URL instead. */
  driveWebViewLink: string | null
  uploadedByUserId: string
  uploadedAt: string
}

/** Which backend holds a file's bytes. Chosen per property; recorded per document. */
export type DocumentStorageKind = 'Blob' | 'Drive'
