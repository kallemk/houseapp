import { useProperties } from './useProperties'

/**
 * The dashboard/pages default to whichever property comes first rather than showing a
 * property-switcher — in practice this will almost always be the one house being tracked.
 */
export function usePrimaryProperty() {
  const { data: properties, isLoading } = useProperties()
  const property = properties?.[0] ?? null
  return { property, properties: properties ?? [], isLoading }
}
