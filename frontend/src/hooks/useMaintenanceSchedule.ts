import { useQuery } from '@tanstack/react-query'
import { maintenanceApi } from '../api/maintenance'

export function useMaintenanceSchedule(propertyId: string) {
  return useQuery({
    queryKey: ['maintenance-schedule', propertyId],
    queryFn: () => maintenanceApi.scheduleForProperty(propertyId),
    enabled: !!propertyId,
  })
}
