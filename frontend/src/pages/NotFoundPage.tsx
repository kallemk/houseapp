import { Button, Center, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <Center h="100vh">
      <Stack align="center">
        <Title>404</Title>
        <Text c="dimmed">Sidan kunde inte hittas.</Text>
        <Button component={Link} to="/">
          Tillbaka till översikten
        </Button>
      </Stack>
    </Center>
  )
}
