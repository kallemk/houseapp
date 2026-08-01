import { AppShell, Container } from '@mantine/core'
import { Outlet } from 'react-router-dom'
import { AppFooter } from './AppFooter'
import { NavBar } from './NavBar'

export function AppLayout() {
  return (
    <AppShell header={{ height: 60 }} padding="md">
      <AppShell.Header style={{ backgroundColor: '#fffaf6', borderBottom: '1px solid var(--mantine-color-gray-2)' }}>
        <NavBar />
      </AppShell.Header>
      <AppShell.Main>
        <Container size="lg" py="md">
          <Outlet />
        </Container>
        <AppFooter />
      </AppShell.Main>
    </AppShell>
  )
}
