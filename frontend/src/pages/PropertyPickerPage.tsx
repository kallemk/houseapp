import {
  Button,
  Card,
  Center,
  Container,
  Group,
  Loader,
  SimpleGrid,
  Stack,
  Text,
  TextInput,
  ThemeIcon,
  Title,
  UnstyledButton,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconHome2, IconHomeStar, IconLogout } from '@tabler/icons-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useCreateProperty, useProperties } from '../hooks/useProperties'
import { setLastPropertyId } from '../utils/lastProperty'

interface PropertyFormValues {
  nickname: string
  address: string
  purchaseDate: string
  purchasePrice: number
}

export function PropertyPickerPage() {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const { data: properties, isLoading } = useProperties()
  const createProperty = useCreateProperty()

  const form = useForm<PropertyFormValues>({
    initialValues: { nickname: '', address: '', purchaseDate: '', purchasePrice: 0 },
  })

  function selectProperty(id: string) {
    setLastPropertyId(id)
    navigate(`/properties/${id}`)
  }

  function handleCreate(values: PropertyFormValues) {
    createProperty.mutate(
      { ...values, purchasePrice: Number(values.purchasePrice) },
      { onSuccess: (created) => selectProperty(created.id) },
    )
  }

  if (isLoading) {
    return (
      <Center h="100vh">
        <Loader />
      </Center>
    )
  }

  const hasProperties = !!properties && properties.length > 0

  return (
    <Container size="sm" py="xl">
      <Group justify="space-between" mb="xl">
        <Group gap="xs">
          <ThemeIcon variant="light" radius="md" size="md">
            <IconHomeStar size={18} />
          </ThemeIcon>
          <Text fw={700}>HusTracker</Text>
        </Group>
        <Button variant="subtle" size="xs" leftSection={<IconLogout size={14} />} onClick={() => logout()}>
          Logga ut
        </Button>
      </Group>

      <Stack gap="xl">
        {hasProperties && (
          <Stack>
            <Title order={3}>Dina bostäder</Title>
            <SimpleGrid cols={{ base: 1, sm: 2 }}>
              {properties.map((property) => (
                <UnstyledButton key={property.id} onClick={() => selectProperty(property.id)}>
                  <Card withBorder padding="lg" h="100%">
                    <Group wrap="nowrap">
                      <ThemeIcon variant="light" size={40} radius="md">
                        <IconHome2 size={20} />
                      </ThemeIcon>
                      <div>
                        <Text fw={600}>{property.nickname}</Text>
                        <Text size="sm" c="dimmed">
                          {property.address}
                        </Text>
                      </div>
                    </Group>
                  </Card>
                </UnstyledButton>
              ))}
            </SimpleGrid>
          </Stack>
        )}

        <Stack>
          <Title order={3}>{hasProperties ? 'Lägg till ny bostad' : 'Lägg till din första bostad'}</Title>
          <Card withBorder padding="lg">
            <form onSubmit={form.onSubmit(handleCreate)}>
              <Stack maw={420}>
                <TextInput label="Smeknamn" required {...form.getInputProps('nickname')} />
                <TextInput label="Adress" required {...form.getInputProps('address')} />
                <TextInput label="Köpdatum" type="date" required {...form.getInputProps('purchaseDate')} />
                <TextInput label="Köpeskilling (kr)" type="number" required {...form.getInputProps('purchasePrice')} />
                <Button type="submit" loading={createProperty.isPending}>
                  Lägg till bostad
                </Button>
              </Stack>
            </form>
          </Card>
        </Stack>
      </Stack>
    </Container>
  )
}
