import { useEffect } from 'react'
import { useProperties } from './useProperties'
import { setLastPropertyId } from '../utils/lastProperty'

/**
 * Resolves the :propertyId route param against the user's own properties (already filtered
 * server-side by membership). `notFound` covers both a bad id and a property the user isn't
 * connected to — the API returns 404 for both, so there's nothing to distinguish client-side.
 */
export function useSelectedProperty(propertyId: string | undefined) {
  const { data: properties, isLoading } = useProperties()
  const property = properties?.find((p) => p.id === propertyId) ?? null
  const notFound = !isLoading && !!propertyId && !property

  useEffect(() => {
    if (property) {
      setLastPropertyId(property.id)
    }
  }, [property])

  return { property, properties: properties ?? [], isLoading, notFound }
}
