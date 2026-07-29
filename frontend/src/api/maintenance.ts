import { apiClient } from './client'
import type { MaintenanceScheduleItemDto } from './types'

/** Read-only: the schedule is derived server-side, there is nothing to write. */
export const maintenanceApi = {
  scheduleForProperty: (propertyId: string) =>
    apiClient.get<MaintenanceScheduleItemDto[]>(`/properties/${propertyId}/maintenance-schedule`),
}
