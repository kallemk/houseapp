import {
  ActionIcon,
  Badge,
  Button,
  Card,
  Center,
  Checkbox,
  Container,
  Group,
  Loader,
  Menu,
  Modal,
  SimpleGrid,
  Stack,
  Text,
  ThemeIcon,
  Title,
  UnstyledButton,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import {
  IconBrandGoogleDrive,
  IconDotsVertical,
  IconEdit,
  IconHome2,
  IconHomeStar,
  IconLogout,
  IconStar,
  IconTrash,
  IconUsers,
} from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { driveApi } from '../api/documents'
import type { PropertyDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PropertyAccessModal } from '../components/properties/PropertyAccessModal'
import { PropertyForm } from '../components/properties/PropertyForm'
import { propertyFormToInput, propertyToFormValues, type PropertyFormValues } from '../utils/propertyForm'
import { useSetDemoProperty } from '../hooks/usePropertyMembers'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useCreateProperty, useDeleteProperty, useProperties, useUpdateProperty } from '../hooks/useProperties'
import { clearLastPropertyId, setLastPropertyId } from '../utils/lastProperty'

export function PropertyPickerPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const { data: properties, isLoading } = useProperties()
  const createProperty = useCreateProperty()
  const updateProperty = useUpdateProperty()
  const deleteProperty = useDeleteProperty()
  const [editing, setEditing] = useState<PropertyDto | null>(null)
  const [managingAccess, setManagingAccess] = useState<PropertyDto | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PropertyDto | null>(null)
  const [alsoDeleteFromDrive, setAlsoDeleteFromDrive] = useState(false)
  const setDemo = useSetDemoProperty()

  function toggleDemo(property: PropertyDto) {
    setDemo.mutate(
      { propertyId: property.id, isDemo: !property.isDemo },
      { onError: () => notifications.show({ color: 'red', message: 'Kunde inte ändra demomarkeringen.' }) },
    )
  }

  function selectProperty(id: string) {
    setLastPropertyId(id)
    navigate(`/properties/${id}`)
  }

  function handleCreate(values: PropertyFormValues) {
    createProperty.mutate(propertyFormToInput(values), { onSuccess: (created) => selectProperty(created.id) })
  }

  function handleUpdate(values: PropertyFormValues) {
    if (!editing) return
    updateProperty.mutate({ id: editing.id, input: propertyFormToInput(values) }, { onSuccess: () => setEditing(null) })
  }

  function handleDelete(property: PropertyDto, deleteFromDrive: boolean) {
    deleteProperty.mutate({ id: property.id, deleteFromDrive }, {
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
                          <Group gap="xs" wrap="nowrap">
                            <Text fw={600} truncate>
                              {property.nickname}
                            </Text>
                            {property.isDemo && (
                              <Badge size="xs" variant="light" color="grape">
                                Demo
                              </Badge>
                            )}
                          </Group>
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
                        {/* Sharing and deleting belong to the people who actually own the property.
                            Someone who only sees it because it's the demo gets neither. */}
                        {property.isMember && (
                          <Menu.Item leftSection={<IconUsers size={14} />} onClick={() => setManagingAccess(property)}>
                            Hantera åtkomst
                          </Menu.Item>
                        )}
                        {/* Same reasoning: connecting binds your own Google account to the property,
                            and every upload afterwards runs on that grant. */}
                        {property.isMember && property.documentStorage === 'Blob' && (
                          <Menu.Item
                            leftSection={<IconBrandGoogleDrive size={14} />}
                            onClick={() => driveApi.connect(property.id)}
                          >
                            Anslut Google Drive
                          </Menu.Item>
                        )}
                        {property.isMember && property.documentStorage === 'Drive' && (
                          <Menu.Item
                            leftSection={<IconBrandGoogleDrive size={14} />}
                            component="a"
                            href={property.driveFolderUrl ?? undefined}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Öppna Drive-mappen
                          </Menu.Item>
                        )}
                        {user?.isAdmin && (
                          <Menu.Item
                            leftSection={<IconStar size={14} />}
                            onClick={() => toggleDemo(property)}
                          >
                            {property.isDemo ? 'Ta bort demomarkering' : 'Markera som demobostad'}
                          </Menu.Item>
                        )}
                        {property.isMember && !property.isDemo && (
                          <Menu.Item
                            color="red"
                            leftSection={<IconTrash size={14} />}
                            onClick={() => setPendingDelete(property)}
                          >
                            Ta bort
                          </Menu.Item>
                        )}
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

      <PropertyAccessModal property={managingAccess} onClose={() => setManagingAccess(null)} />

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera bostad" size="lg" centered>
        {editing && (
          <PropertyForm
            key={editing.id}
            initial={propertyToFormValues(editing)}
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
        onCancel={() => {
          setPendingDelete(null)
          setAlsoDeleteFromDrive(false)
        }}
        onConfirm={() => {
          if (pendingDelete) {
            handleDelete(pendingDelete, alsoDeleteFromDrive)
          }
          setPendingDelete(null)
          setAlsoDeleteFromDrive(false)
        }}
      >
        {/* Off by default, and asked rather than assumed — this would otherwise clear a folder's
            worth of files out of someone's personal Drive as a side effect. */}
        {pendingDelete?.documentStorage === 'Drive' && (
          <Checkbox
            label="Ta bort även bostadens filer från Google Drive"
            checked={alsoDeleteFromDrive}
            onChange={(e) => setAlsoDeleteFromDrive(e.currentTarget.checked)}
          />
        )}
      </ConfirmDialog>
    </Container>
  )
}
