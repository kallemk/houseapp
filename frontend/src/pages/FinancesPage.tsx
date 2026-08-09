import { Group, Stack, Tabs, Text, ThemeIcon, Title } from '@mantine/core'
import { IconChartLine, IconClockDollar, IconCoins, IconPigMoney, IconWallet } from '@tabler/icons-react'
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'

const TABS = [
  { value: 'budget', label: 'Budget', icon: IconWallet },
  { value: 'spending', label: 'Utgifter', icon: IconCoins },
  { value: 'upcoming', label: 'Kommande', icon: IconClockDollar },
  { value: 'valuations', label: 'Värdering', icon: IconChartLine },
]

/**
 * Everything about the money, in the order you'd ask about it: what you planned to spend, what you
 * have spent, what is still ahead of you, and what the house is worth.
 *
 * These four used to be spread over the dashboard and the budget page with no rule about which held
 * what — three spend components on the overview, two more here. Grouping them means the overview can
 * carry a summary and link here, rather than half-answering the question itself.
 */
export function FinancesPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const navigate = useNavigate()
  const location = useLocation()

  // The last path segment is the tab. `index` redirects to /budget, so there's always one.
  const active = TABS.find((tab) => location.pathname.endsWith(`/${tab.value}`))?.value ?? null

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconPigMoney size={20} />
        </ThemeIcon>
        <div>
          <Title order={2}>Ekonomi</Title>
          <Text size="sm" c="dimmed">
            Vad du planerat, vad det har kostat, vad som väntar — och vad bostaden är värd.
          </Text>
        </div>
      </Group>

      <Tabs value={active} onChange={(value) => navigate(`/properties/${propertyId}/finances/${value}`)}>
        <Tabs.List>
          {TABS.map((tab) => (
            <Tabs.Tab key={tab.value} value={tab.value} leftSection={<tab.icon size={16} />}>
              {tab.label}
            </Tabs.Tab>
          ))}
        </Tabs.List>
      </Tabs>

      <Outlet />
    </Stack>
  )
}
