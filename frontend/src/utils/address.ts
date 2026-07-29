import type { PropertyDto } from '../api/types'

/**
 * "Tulebovägen 64, Lgh 1101, 434 33 Kungsbacka, Sverige" — every part is optional, since properties
 * created before these fields existed have none of them. Blank parts are dropped rather than
 * leaving stray commas.
 */
export function formatAddress(property: PropertyDto): string {
  const postalAndCity = [property.postalCode, property.city].filter(Boolean).join(' ')
  return [property.address, property.address2, postalAndCity, property.country]
    .map((part) => part?.trim())
    .filter((part) => !!part)
    .join(', ')
}
