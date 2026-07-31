import { Anchor, Card, Group, Text } from '@mantine/core'
import { IconMapPin } from '@tabler/icons-react'

/**
 * OpenStreetMap's public embed, in a plain iframe.
 *
 * No map library and no API key: Leaflet would add a dependency for a static single-marker view,
 * and every keyed provider (Google, Azure Maps, Mapbox) means a billing account or another secret
 * to deploy for what is one pin on one house. This is free, keyless and needs nothing in Bicep.
 */
export function PropertyMap({
  latitude,
  longitude,
  label,
}: {
  latitude: number
  longitude: number
  label: string
}) {
  // OSM's embed takes a bounding box, not a zoom level, and fits that box inside the iframe while
  // preserving its shape — so a box whose shape doesn't match the iframe gets letterboxed into a
  // sliver, or nothing at all on a narrow screen. The two have to be derived from one number.
  //
  // In Web Mercator, a degree of longitude is a fixed screen width, while a degree of latitude
  // covers 1/cos(latitude) as much screen height. So for an iframe of aspect ratio R:
  //   deltaLon = R * deltaLat / cos(latitude)
  // At Swedish latitudes cos(φ) is around 0.5, so the longitude span is roughly twice what a naive
  // calculation would give — which is exactly why the old fixed box came out square.
  const aspectRatio = 16 / 7
  const deltaLat = 0.005 // ≈ 550 m tall
  const deltaLon = (aspectRatio * deltaLat) / Math.cos((latitude * Math.PI) / 180)

  const bbox = [
    longitude - deltaLon / 2,
    latitude - deltaLat / 2,
    longitude + deltaLon / 2,
    latitude + deltaLat / 2,
  ].join(',')
  const src = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${latitude},${longitude}`

  return (
    <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
      <iframe
        title={`Karta – ${label}`}
        src={src}
        // aspectRatio rather than a fixed height, so the frame keeps the shape the bbox was built
        // for at every width.
        style={{ border: 0, width: '100%', aspectRatio, display: 'block' }}
        loading="lazy"
        referrerPolicy="no-referrer-when-downgrade"
      />
      <Group justify="space-between" p="xs">
        <Group gap={4}>
          <IconMapPin size={14} />
          <Text size="xs" c="dimmed">
            {latitude.toFixed(5)}, {longitude.toFixed(5)}
          </Text>
        </Group>
        <Anchor
          href={`https://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=17/${latitude}/${longitude}`}
          target="_blank"
          rel="noreferrer"
          size="xs"
        >
          Öppna större karta
        </Anchor>
      </Group>
    </Card>
  )
}
