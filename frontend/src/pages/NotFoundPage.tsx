import { Button, Center, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <Center h="100vh">
      <Stack align="center">
        <Title>404</Title>
        <Text c="dimmed">Page not found.</Text>
        <Button component={Link} to="/">
          Back to dashboard
        </Button>
      </Stack>
    </Center>
  )
}
