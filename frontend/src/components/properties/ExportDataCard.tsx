import { Button, Card, Group, Text, ThemeIcon } from '@mantine/core'
import { IconDownload } from '@tabler/icons-react'
import { propertiesApi } from '../../api/properties'
import type { PropertyDto } from '../../api/types'

/**
 * A standing reminder that nothing logged here is locked in — the reason it's worth the effort.
 * Sits at the bottom of Översikt and Projekt, deliberately: it's a footnote, not a figure, and the
 * overview must not push its numbers down for it. Always shown rather than dismissible, so it adds
 * no browser storage that CookiesPage would have to declare.
 */
export function ExportDataCard({ property }: { property: PropertyDto }) {
  return (
    <Card withBorder padding="md" mt="lg">
      <Group justify="space-between" wrap="wrap" gap="md">
        <Group gap="sm" wrap="nowrap" align="flex-start" style={{ flex: '1 1 320px', minWidth: 0 }}>
          <ThemeIcon variant="light" size={36} radius="md">
            <IconDownload size={20} />
          </ThemeIcon>
          <div style={{ minWidth: 0 }}>
            <Text fw={600}>Din data är din</Text>
            <Text size="sm" c="dimmed">
              Ta med allt om {property.nickname} – projekt, kostnader, värderingar, budget och dokument – som
              JSON, Excel och originalfiler. Slutar du använda HusTracker har du allt kvar.
            </Text>
          </div>
        </Group>
        <Button variant="light" leftSection={<IconDownload size={16} />} onClick={() => propertiesApi.export(property.id)}>
          Exportera all data
        </Button>
      </Group>
    </Card>
  )
}
