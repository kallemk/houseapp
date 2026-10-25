import { Button, Card, Group, Text, ThemeIcon } from '@mantine/core'
import { IconPlugConnected } from '@tabler/icons-react'
import { useNavigate } from 'react-router-dom'
import { useClaudeConnections } from '../../hooks/useClaudeConnections'

/**
 * Tells people Claude can be connected, on Översikt, until they have connected it. Showing it
 * only while there's no connection is what makes it go away — no "Dölj" button and nothing stored
 * in the browser, so CookiesPage stays true. Like ExportDataCard it sits at the bottom: a hint, not
 * a figure. Renders nothing while loading or on error, rather than flashing in and out.
 */
export function ClaudeHintCard() {
  const navigate = useNavigate()
  const { data: connections, isSuccess } = useClaudeConnections()

  if (!isSuccess || connections.length > 0) {
    return null
  }

  return (
    <Card withBorder padding="md" mt="lg">
      <Group justify="space-between" wrap="wrap" gap="md">
        <Group gap="sm" wrap="nowrap" align="flex-start" style={{ flex: '1 1 320px', minWidth: 0 }}>
          <ThemeIcon variant="light" size={36} radius="md" color="violet">
            <IconPlugConnected size={20} />
          </ThemeIcon>
          <div style={{ minWidth: 0 }}>
            <Text fw={600}>Tips: planera med Claude</Text>
            <Text size="sm" c="dimmed">
              Koppla HusTracker till Claude, så kan du be Claude lägga in projekt, kostnader och milstolpar direkt här
              medan ni planerar – utan att skriva av något.
            </Text>
          </div>
        </Group>
        <Button variant="light" color="violet" onClick={() => navigate('/claude')}>
          Så kopplar du
        </Button>
      </Group>
    </Card>
  )
}
