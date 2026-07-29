// Swedish display labels for backend enum values. The enum values themselves (the API contract)
// stay in English — only what the user sees is translated here.
//
// Property components are not an enum — their Swedish names are admin-managed data (see
// PropertyComponentsPage), not hardcoded here.
import type {
  CostType,
  DocumentCategory,
  MaintenanceUrgency,
  ProjectPriority,
  ProjectStatus,
  PropertyType,
  WorkType,
} from '../api/types'

function toOptions<T extends string>(labels: Record<T, string>) {
  return (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }))
}

export const DOCUMENT_CATEGORY_LABELS: Record<DocumentCategory, string> = {
  Deed: 'Lagfart',
  Warranty: 'Garanti',
  Receipt: 'Kvitto',
  Photo: 'Foto',
  Other: 'Övrigt',
}

export const DOCUMENT_CATEGORY_OPTIONS = toOptions(DOCUMENT_CATEGORY_LABELS)

export const WORK_TYPE_LABELS: Record<WorkType, string> = {
  Maintenance: 'Underhåll',
  Renovation: 'Renovering',
  Investment: 'Nyinvestering',
}

export const WORK_TYPE_OPTIONS = toOptions(WORK_TYPE_LABELS)

export const WORK_TYPE_COLORS: Record<WorkType, string> = {
  Maintenance: 'blue',
  Renovation: 'terracotta',
  Investment: 'teal',
}

export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  Planned: 'Planerad',
  InProgress: 'Pågående',
  Completed: 'Slutförd',
  OnHold: 'Uppskjuten',
  Cancelled: 'Avbruten',
}

export const PROJECT_STATUS_OPTIONS = toOptions(PROJECT_STATUS_LABELS)

export const PROJECT_STATUS_COLORS: Record<ProjectStatus, string> = {
  Planned: 'gray',
  InProgress: 'blue',
  Completed: 'green',
  OnHold: 'yellow',
  Cancelled: 'red',
}

export const PRIORITY_LABELS: Record<ProjectPriority, string> = {
  Low: 'Låg',
  Medium: 'Medel',
  High: 'Hög',
  Critical: 'Kritisk',
}

export const PRIORITY_OPTIONS = toOptions(PRIORITY_LABELS)

export const PRIORITY_COLORS: Record<ProjectPriority, string> = {
  Low: 'gray',
  Medium: 'blue',
  High: 'orange',
  Critical: 'red',
}

export const COST_TYPE_LABELS: Record<CostType, string> = {
  Materials: 'Material',
  Labor: 'Arbetskraft',
  Tools: 'Verktyg',
  Permits: 'Tillstånd',
  Other: 'Övrigt',
}

export const COST_TYPE_OPTIONS = toOptions(COST_TYPE_LABELS)

export const PROPERTY_TYPE_LABELS: Record<PropertyType, string> = {
  House: 'Villa',
  Apartment: 'Lägenhet',
  Townhouse: 'Radhus',
  Cottage: 'Fritidshus',
  Other: 'Övrigt',
}

export const PROPERTY_TYPE_OPTIONS = toOptions(PROPERTY_TYPE_LABELS)

export const MAINTENANCE_URGENCY_LABELS: Record<MaintenanceUrgency, string> = {
  Overdue: 'Försenat',
  DueSoon: 'Snart',
  Ok: 'Ok',
  Unknown: 'Aldrig loggat',
  NotScheduled: 'Inget intervall',
}

export const MAINTENANCE_URGENCY_COLORS: Record<MaintenanceUrgency, string> = {
  Overdue: 'red',
  DueSoon: 'orange',
  Ok: 'green',
  Unknown: 'gray',
  NotScheduled: 'gray',
}
