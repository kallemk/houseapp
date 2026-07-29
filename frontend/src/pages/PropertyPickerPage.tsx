import {
  ActionIcon,
  Button,
  Card,
  Center,
  Container,
  Group,
  Loader,
  Menu,
  Modal,
  NumberInput,
  Select,
  SimpleGrid,
  Stack,
  Text,
  TextInput,
  ThemeIcon,
  Title,
  UnstyledButton,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconDotsVertical, IconEdit, IconHome2, IconHomeStar, IconLogout, IconMapPin, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { PropertyDto, PropertyType } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PROPERTY_TYPE_OPTIONS } from '../utils/labels'
import { geocodeAddress } from '../utils/geocode'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useCreateProperty, useDeleteProperty, useProperties, useUpdateProperty } from '../hooks/useProperties'
import { clearLastPropertyId, setLastPropertyId } from '../utils/lastProperty'

interface PropertyFormValues {
  nickname: string
  address: string
  address2: string
  postalCode: string
  city: string
  country: string
  propertyDesignation: string
  yearBuilt: number | string
  type: PropertyType | ''
  latitude: number | string
  longitude: number | string
  purchaseDate: string
  purchasePrice: number | string
}

const EMPTY_FORM: PropertyFormValues = {
  nickname: '',
  address: '',
  address2: '',
  postalCode: '',
  city: '',
  country: 'Sverige',
  propertyDesignation: '',
  yearBuilt: '',
  type: '',
  latitude: '',
  longitude: '',
  purchaseDate: '',
  purchasePrice: '',
}

function PropertyForm({
  initial,
  submitLabel,
  onSubmit,
  submitting,
}: {
  initial?: PropertyFormValues
  submitLabel: string
  onSubmit: (values: PropertyFormValues) => void
  submitting: boolean
}) {
  const form = useForm<PropertyFormValues>({ initialValues: initial ?? EMPTY_FORM })
  const [locating, setLocating] = useState(false)

  async function lookUpCoordinates() {
    const query = [form.values.address, form.values.postalCode, form.values.city, form.values.country]
      .map((part) => part.trim())
      .filter(Boolean)
      .join(', ')
    if (!query) {
      return
    }

    setLocating(true)
    try {
      const result = await geocodeAddress(query)
      if (result) {
        form.setFieldValue('latitude', result.latitude)
        form.setFieldValue('longitude', result.longitude)
        notifications.show({ color: 'green', message: `Hittade: ${result.displayName}` })
      } else {
        notifications.show({ color: 'yellow', message: 'Hittade ingen plats för adressen. Fyll i koordinaterna manuellt.' })
      }
    } catch {
      notifications.show({ color: 'red', message: 'Kunde inte slå upp adressen just nu. Fyll i koordinaterna manuellt.' })
    } finally {
      setLocating(false)
    }
  }

  return (
    <form onSubmit={form.onSubmit(onSubmit)}>
      <Stack maw={420}>
        <TextInput label="Smeknamn" required {...form.getInputProps('nickname')} />
        <TextInput label="Adress" required {...form.getInputProps('address')} />
        <TextInput label="Adressrad 2" placeholder="valfritt" {...form.getInputProps('address2')} />
        <Group grow>
          <TextInput label="Postnummer" placeholder="valfritt" {...form.getInputProps('postalCode')} />
          <TextInput label="Ort" placeholder="valfritt" {...form.getInputProps('city')} />
        </Group>
        <TextInput label="Land" placeholder="valfritt" {...form.getInputProps('country')} />
        <TextInput
          label="Fastighetsbeteckning"
          placeholder="valfritt"
          {...form.getInputProps('propertyDesignation')}
        />
        <Group grow>
          <Select
            label="Bostadstyp"
            placeholder="valfritt"
            data={PROPERTY_TYPE_OPTIONS}
            clearable
            {...form.getInputProps('type')}
          />
          <NumberInput label="Byggår" placeholder="valfritt" min={1500} max={2100} {...form.getInputProps('yearBuilt')} />
        </Group>
        <Group grow>
          <NumberInput
            label="Latitud"
            placeholder="valfritt"
            decimalScale={6}
            {...form.getInputProps('latitude')}
          />
          <NumberInput
            label="Longitud"
            placeholder="valfritt"
            decimalScale={6}
            {...form.getInputProps('longitude')}
          />
        </Group>
        {/* Fills the two fields above from the address; they stay editable and are what's stored. */}
        <Button
          variant="light"
          leftSection={<IconMapPin size={16} />}
          onClick={lookUpCoordinates}
          loading={locating}
          disabled={!form.values.address.trim()}
        >
          Hämta koordinater från adressen
        </Button>
        <TextInput label="Köpdatum" type="date" required {...form.getInputProps('purchaseDate')} />
        <TextInput label="Köpeskilling (kr)" type="number" required {...form.getInputProps('purchasePrice')} />
        <Button type="submit" loading={submitting}>
          {submitLabel}
        </Button>
      </Stack>
    </form>
  )
}

export function PropertyPickerPage() {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const { data: properties, isLoading } = useProperties()
  const createProperty = useCreateProperty()
  const updateProperty = useUpdateProperty()
  const deleteProperty = useDeleteProperty()
  const [editing, setEditing] = useState<PropertyDto | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PropertyDto | null>(null)

  function selectProperty(id: string) {
    setLastPropertyId(id)
    navigate(`/properties/${id}`)
  }

  function toInput(values: PropertyFormValues) {
    return {
      nickname: values.nickname,
      address: values.address,
      address2: values.address2.trim() || null,
      postalCode: values.postalCode.trim() || null,
      city: values.city.trim() || null,
      country: values.country.trim() || null,
      propertyDesignation: values.propertyDesignation.trim() || null,
      yearBuilt: values.yearBuilt === '' ? null : Number(values.yearBuilt),
      type: values.type === '' ? null : values.type,
      latitude: values.latitude === '' ? null : Number(values.latitude),
      longitude: values.longitude === '' ? null : Number(values.longitude),
      purchaseDate: values.purchaseDate,
      purchasePrice: Number(values.purchasePrice),
    }
  }

  function handleCreate(values: PropertyFormValues) {
    createProperty.mutate(toInput(values), { onSuccess: (created) => selectProperty(created.id) })
  }

  function handleUpdate(values: PropertyFormValues) {
    if (!editing) return
    updateProperty.mutate({ id: editing.id, input: toInput(values) }, { onSuccess: () => setEditing(null) })
  }

  function handleDelete(property: PropertyDto) {
    deleteProperty.mutate(property.id, {
      // The deleted property may well be the one "/" would jump back into on next load.
      onSuccess: () => clearLastPropertyId(property.id),
      onError: () => notifications.show({ color: 'red', message: 'Bostaden kunde inte tas bort. Försök igen.' }),
    })
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
                // The card body is the clickable target and the menu sits outside it — nesting the
                // menu button inside the card-wide UnstyledButton would be a button inside a button.
                <Card key={property.id} withBorder padding="lg" h="100%">
                  <Group wrap="nowrap" align="flex-start" justify="space-between">
                    <UnstyledButton onClick={() => selectProperty(property.id)} style={{ flex: 1, minWidth: 0 }}>
                      <Group wrap="nowrap">
                        <ThemeIcon variant="light" size={40} radius="md">
                          <IconHome2 size={20} />
                        </ThemeIcon>
                        <div style={{ minWidth: 0 }}>
                          <Text fw={600} truncate>
                            {property.nickname}
                          </Text>
                          <Text size="sm" c="dimmed" truncate>
                            {property.address}
                          </Text>
                        </div>
                      </Group>
                    </UnstyledButton>

                    <Menu position="bottom-end" withArrow shadow="md">
                      <Menu.Target>
                        <ActionIcon variant="subtle" color="gray" aria-label="Hantera bostad">
                          <IconDotsVertical size={16} />
                        </ActionIcon>
                      </Menu.Target>
                      <Menu.Dropdown>
                        <Menu.Item leftSection={<IconEdit size={14} />} onClick={() => setEditing(property)}>
                          Redigera
                        </Menu.Item>
                        <Menu.Item
                          color="red"
                          leftSection={<IconTrash size={14} />}
                          onClick={() => setPendingDelete(property)}
                        >
                          Ta bort
                        </Menu.Item>
                      </Menu.Dropdown>
                    </Menu>
                  </Group>
                </Card>
              ))}
            </SimpleGrid>
          </Stack>
        )}

        <Stack>
          <Title order={3}>{hasProperties ? 'Lägg till ny bostad' : 'Lägg till din första bostad'}</Title>
          <Card withBorder padding="lg">
            <PropertyForm submitLabel="Lägg till bostad" onSubmit={handleCreate} submitting={createProperty.isPending} />
          </Card>
        </Stack>
      </Stack>

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera bostad" centered>
        {editing && (
          <PropertyForm
            key={editing.id}
            initial={{
              nickname: editing.nickname,
              address: editing.address,
              address2: editing.address2 ?? '',
              postalCode: editing.postalCode ?? '',
              city: editing.city ?? '',
              country: editing.country ?? '',
              propertyDesignation: editing.propertyDesignation ?? '',
              yearBuilt: editing.yearBuilt ?? '',
              type: editing.type ?? '',
              latitude: editing.latitude ?? '',
              longitude: editing.longitude ?? '',
              purchaseDate: editing.purchaseDate,
              purchasePrice: editing.purchasePrice,
            }}
            submitLabel="Spara"
            onSubmit={handleUpdate}
            submitting={updateProperty.isPending}
          />
        )}
      </Modal>

      <ConfirmDialog
        opened={pendingDelete !== null}
        title="Ta bort bostad"
        message={
          pendingDelete
            ? `"${pendingDelete.nickname}" tas bort tillsammans med alla värderingar, renoveringar och dokument som hör till bostaden. Detta kan inte ångras.`
            : ''
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) {
            handleDelete(pendingDelete)
          }
          setPendingDelete(null)
        }}
      />
    </Container>
  )
}
