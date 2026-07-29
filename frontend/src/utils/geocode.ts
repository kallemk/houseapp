/**
 * Address → coordinates via OpenStreetMap's Nominatim. Free and keyless, which is why it's used
 * here over Azure Maps or Google (both need a billing account and a secret to deploy).
 *
 * Deliberately a manual, one-off lookup behind a button rather than something that runs on save:
 * Nominatim asks callers to keep to roughly one request a second, results for a Swedish address
 * are good but not guaranteed, and the coordinates never change once they're right. The stored
 * lat/long is the source of truth — this only fills the fields in, and they stay editable.
 */
export interface GeocodeResult {
  latitude: number
  longitude: number
  displayName: string
}

export async function geocodeAddress(query: string): Promise<GeocodeResult | null> {
  const url = new URL('https://nominatim.openstreetmap.org/search')
  url.searchParams.set('q', query)
  url.searchParams.set('format', 'jsonv2')
  url.searchParams.set('limit', '1')

  const response = await fetch(url, { headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error(`Geocoding failed: ${response.status}`)
  }

  const results = (await response.json()) as { lat: string; lon: string; display_name: string }[]
  if (results.length === 0) {
    return null
  }

  return {
    latitude: Number(results[0].lat),
    longitude: Number(results[0].lon),
    displayName: results[0].display_name,
  }
}
