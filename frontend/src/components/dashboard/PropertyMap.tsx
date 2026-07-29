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
  // A small box around the point — OSM's embed takes a bounding box rather than a zoom level.
  const span = 0.004
  const bbox = [longitude - span, latitude - span / 2, longitude + span, latitude + span / 2].join(',')
  const src = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${latitude},${longitude}`

  return (
    <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
      <iframe
        title={`Karta – ${label}`}
        src={src}
        style={{ border: 0, width: '100%', height: 280, display: 'block' }}
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
