import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Card,
  Code,
  Container,
  CopyButton,
  Group,
  List,
  SimpleGrid,
  Stack,
  Table,
  Text,
  ThemeIcon,
  Timeline,
  Title,
  Tooltip,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import {
  IconBulb,
  IconCheck,
  IconCopy,
  IconLock,
  IconMessageChatbot,
  IconPlugConnected,
} from '@tabler/icons-react'
import { mcpServerUrl } from '../api/claude'
import { EmptyState } from '../components/common/EmptyState'
import { useClaudeConnections, useDisconnectClaude } from '../hooks/useClaudeConnections'

/** Things worth asking once connected — concrete, so the first conversation doesn't start from a blank box. */
const EXAMPLES = [
  'Vilka underhållsåtgärder är försenade i huset?',
  'Lägg in takbytet som ett planerat renoveringsprojekt för våren, uppskattat till 180 000 kr.',
  'Jag har fått en offert från Takfirman AB på 165 000 kr – lägg in den på takprojektet och ge mig en länk så jag kan ladda upp PDF:en.',
  'Hur ligger vi till mot årets budget för underhåll?',
  'Dela upp fasadmålningen i milstolpar: skrapa, grunda, måla, städa.',
  'Byt kategori till Faktura på dokumentet ”scan_0042”.',
]

/**
 * The guide to connecting Claude, and the connections already made. Reached from the account menu
 * and from the hint on Översikt. Disconnecting takes effect on Claude's very next call — the server
 * checks the connection on every request.
 */
export function ClaudeConnectionPage() {
  const { data: connections } = useClaudeConnections()
  const disconnect = useDisconnectClaude()
  const url = mcpServerUrl()
  const connected = (connections ?? []).length > 0

  return (
    <Container size="sm" p={0}>
      <Stack>
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md" color="violet">
            <IconPlugConnected size={20} />
          </ThemeIcon>
          <Title order={2}>Koppla till Claude</Title>
          {connected && (
            <Badge color="teal" variant="light">
              Kopplad
            </Badge>
          )}
        </Group>
        <Text c="dimmed" size="sm">
          Planerar du husprojekten i Claude? Koppla HusTracker så kan Claude läsa och lägga in projekt, kostnader,
          milstolpar och dokumentuppgifter direkt här — utan att du skriver av något. Det tar ungefär en minut.
        </Text>

        <Card withBorder padding="lg">
          <Stack gap="md">
            <Title order={5}>Så kopplar du – steg för steg</Title>
            <Timeline active={-1} bulletSize={24} lineWidth={2}>
              <Timeline.Item bullet={<Text size="xs">1</Text>} title="Öppna Connectors i Claude">
                <Text size="sm" c="dimmed">
                  På claude.ai eller i Claude-appen: <b>Inställningar</b> → <b>Connectors</b> →{' '}
                  <b>Add custom connector</b>.
                </Text>
              </Timeline.Item>
              <Timeline.Item bullet={<Text size="xs">2</Text>} title="Klistra in adressen">
                <Text size="sm" c="dimmed">
                  Döp den till <b>HusTracker</b> och klistra in:
                </Text>
                <Group gap="xs" mt={6} wrap="nowrap">
                  <Code style={{ overflowWrap: 'anywhere' }}>{url}</Code>
                  <CopyButton value={url}>
                    {({ copied, copy }) => (
                      <Tooltip label={copied ? 'Kopierad' : 'Kopiera'}>
                        <ActionIcon
                          variant="subtle"
                          color={copied ? 'teal' : 'gray'}
                          onClick={copy}
                          aria-label="Kopiera adressen"
                        >
                          {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
                        </ActionIcon>
                      </Tooltip>
                    )}
                  </CopyButton>
                </Group>
                <Text size="xs" c="dimmed" mt={4}>
                  Lämna fälten för OAuth client ID och secret tomma.
                </Text>
              </Timeline.Item>
              <Timeline.Item bullet={<Text size="xs">3</Text>} title="Logga in och godkänn">
                <Text size="sm" c="dimmed">
                  Klicka <b>Connect</b>. Du skickas hit, loggar in som vanligt om det behövs och trycker{' '}
                  <b>Godkänn</b>. Sedan är du tillbaka i Claude.
                </Text>
              </Timeline.Item>
              <Timeline.Item bullet={<Text size="xs">4</Text>} title="Slå på den i chatten">
                <Text size="sm" c="dimmed">
                  Aktivera HusTracker under verktygsmenyn i en chatt – eller i ett <b>Claude-projekt</b> där du
                  planerar huset, så finns den med i alla chattar där.
                </Text>
              </Timeline.Item>
            </Timeline>
          </Stack>
        </Card>

        <Card withBorder padding="lg">
          <Stack gap="sm">
            <Group gap="xs">
              <IconMessageChatbot size={18} />
              <Title order={5}>Prova att fråga</Title>
            </Group>
            <List size="sm" spacing={6} icon={<IconBulb size={14} />}>
              {EXAMPLES.map((example) => (
                <List.Item key={example}>
                  <Text size="sm" fs="italic">
                    ”{example}”
                  </Text>
                </List.Item>
              ))}
            </List>
          </Stack>
        </Card>

        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <Alert color="gray" variant="light" icon={<IconLock size={16} />} title="Vad Claude kommer åt">
            <Text size="sm">
              Bara dina egna bostäder – aldrig demobostaden. Claude kan läsa och ändra, men aldrig radera något.
            </Text>
          </Alert>
          <Alert color="gray" variant="light" icon={<IconBulb size={16} />} title="Om filer">
            <Text size="sm">
              Filer kan inte skickas via Claude. Be om en uppladdningslänk – den öppnar uppladdningen här med titel
              och kategori ifyllda.
            </Text>
          </Alert>
        </SimpleGrid>

        <Card withBorder padding="lg">
          <Stack gap="sm">
            <Title order={5}>Dina kopplingar</Title>
            {connected ? (
              <Table.ScrollContainer minWidth={480}>
                <Table verticalSpacing="xs">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Klient</Table.Th>
                      <Table.Th>Kopplad</Table.Th>
                      <Table.Th>Senast använd</Table.Th>
                      <Table.Th />
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {connections!.map((c) => (
                      <Table.Tr key={c.id}>
                        <Table.Td>
                          {c.clientName}
                          <Text size="xs" c="dimmed">
                            {c.redirectHost}
                          </Text>
                        </Table.Td>
                        <Table.Td>{c.createdAt.slice(0, 10)}</Table.Td>
                        <Table.Td>{c.lastUsedAt.slice(0, 10)}</Table.Td>
                        <Table.Td>
                          <Button
                            size="xs"
                            variant="light"
                            color="red"
                            loading={disconnect.isPending && disconnect.variables === c.id}
                            onClick={() =>
                              disconnect.mutate(c.id, {
                                onError: () =>
                                  notifications.show({ color: 'red', message: 'Kunde inte koppla från. Försök igen.' }),
                              })
                            }
                          >
                            Koppla från
                          </Button>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            ) : (
              <EmptyState icon={IconPlugConnected} message="Ingen Claude är kopplad än – följ stegen ovan." />
            )}
          </Stack>
        </Card>
      </Stack>
    </Container>
  )
}
