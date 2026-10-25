import { Alert, Button, Card, Center, Container, Group, List, Loader, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconAlertTriangle, IconPlugConnected } from '@tabler/icons-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { claudeApi } from '../api/claude'
import { useAuth } from '../auth/AuthContext'

/**
 * Where Claude sends the user when they add HusTracker as a connector. The server's /oauth/authorize
 * has already checked the request and packed it into `request`; this page only asks the person and
 * passes the answer on. Outside the app shell, like the login page: it's a step in someone else's
 * flow, and the navbar would invite wandering off half-way.
 *
 * It names where the user is about to be sent, as the MCP spec requires, and warns when that's this
 * computer rather than claude.ai — any local program can listen there.
 */
export function OAuthConsentPage() {
  const [params] = useSearchParams()
  const request = params.get('request') ?? ''
  const { user } = useAuth()
  const [sending, setSending] = useState(false)
  const [failed, setFailed] = useState(false)

  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ['oauth-consent', request],
    queryFn: () => claudeApi.consent(request),
    enabled: request !== '',
    retry: false,
  })

  async function decide(approve: boolean) {
    setSending(true)
    try {
      const { redirectUrl } = await claudeApi.decide(request, approve)
      window.location.href = redirectUrl
    } catch {
      setFailed(true)
      setSending(false)
    }
  }

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  return (
    <Container size="xs" py="xl">
      <Card withBorder padding="xl">
        <Stack>
          <Group gap="sm">
            <ThemeIcon variant="light" size={40} radius="md" color="violet">
              <IconPlugConnected size={22} />
            </ThemeIcon>
            <Title order={3}>Koppla {summary?.clientName ?? 'Claude'} till HusTracker</Title>
          </Group>

          {!summary || isError || failed ? (
            <Alert color="red" icon={<IconAlertTriangle size={16} />}>
              Begäran har gått ut eller är ogiltig. Gå tillbaka till Claude och lägg till kopplingen igen.
            </Alert>
          ) : (
            <>
              <Text size="sm">
                Inloggad som <b>{user?.displayName}</b>. {summary.clientName} får då, för dina egna bostäder:
              </Text>
              <List size="sm" spacing={4}>
                <List.Item>läsa projekt, komponenter, underhållsplan, budget och dokumentlistor</List.Item>
                <List.Item>skapa och ändra projekt, lägga till kostnader och milstolpar</List.Item>
                <List.Item>ändra dokuments titel, datum, kategori och projektkoppling</List.Item>
              </List>
              <Text size="sm" c="dimmed">
                Ingenting kan raderas den här vägen, och demobostaden ingår inte. Du kan koppla från när som
                helst under <b>Koppla till Claude</b> i kontomenyn.
              </Text>

              {summary.isLoopback ? (
                <Alert color="orange" icon={<IconAlertTriangle size={16} />}>
                  Svaret skickas till <b>den här datorn</b> ({summary.redirectHost}), inte till claude.ai. Det är
                  rätt för Claude Code — godkänn bara om du själv startade kopplingen härifrån.
                </Alert>
              ) : (
                <Text size="sm">
                  Du skickas sedan tillbaka till <b>{summary.redirectHost}</b>.
                </Text>
              )}

              <Group justify="flex-end">
                <Button variant="default" onClick={() => decide(false)} disabled={sending}>
                  Avbryt
                </Button>
                <Button onClick={() => decide(true)} loading={sending}>
                  Godkänn
                </Button>
              </Group>
            </>
          )}
        </Stack>
      </Card>
    </Container>
  )
}
