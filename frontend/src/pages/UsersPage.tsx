import {
  ActionIcon,
  Badge,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Modal,
  PasswordInput,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconEdit, IconTrash, IconUsers } from '@tabler/icons-react'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { UserDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useCreateUser, useDeleteUser, useUpdateUser, useUsers } from '../hooks/useUsers'

interface CreateFormValues {
  email: string
  displayName: string
  initialPassword: string
}

export function UsersPage() {
  const { user: currentUser } = useAuth()
  const { data: users, isLoading } = useUsers()
  const createUser = useCreateUser()
  const updateUser = useUpdateUser()
  const deleteUser = useDeleteUser()
  const [editing, setEditing] = useState<UserDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const createForm = useForm<CreateFormValues>({
    initialValues: { email: '', displayName: '', initialPassword: '' },
    validate: {
      email: (value) => (value.trim().length === 0 ? 'E-post krävs' : null),
      displayName: (value) => (value.trim().length === 0 ? 'Namn krävs' : null),
    },
  })

  const editForm = useForm<{ displayName: string }>({ initialValues: { displayName: '' } })

  function showError(error: unknown, conflictMessage: string) {
    notifications.show({
      color: 'red',
      message: error instanceof ApiError && error.status === 409 ? conflictMessage : 'Något gick fel. Försök igen.',
    })
  }

  function handleCreate(values: CreateFormValues) {
    createUser.mutate(
      {
        email: values.email.trim(),
        displayName: values.displayName.trim(),
        initialPassword: values.initialPassword.trim() || null,
      },
      {
        onSuccess: () => createForm.reset(),
        onError: (error) => showError(error, 'Det finns redan en användare med den e-postadressen.'),
      },
    )
  }

  function startEditing(user: UserDto) {
    editForm.setValues({ displayName: user.displayName })
    setEditing(user)
  }

  function handleUpdate(values: { displayName: string }) {
    if (!editing) return
    updateUser.mutate(
      { id: editing.id, input: { displayName: values.displayName, isAdmin: editing.isAdmin } },
      { onSuccess: () => setEditing(null), onError: (error) => showError(error, '') },
    )
  }

  function handleToggleAdmin(user: UserDto) {
    // displayName is sent along because the endpoint takes the whole record. It comes from the same
    // query that renders this row, so there's nothing stale to clobber.
    updateUser.mutate(
      { id: user.id, input: { displayName: user.displayName, isAdmin: !user.isAdmin } },
      { onError: (error) => showError(error, 'Du kan inte ta bort dina egna adminrättigheter.') },
    )
  }

  function handleDelete(id: string) {
    deleteUser.mutate(id, {
      onError: (error) => showError(error, 'Du kan inte ta bort ditt eget konto.'),
    })
  }

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconUsers size={20} />
        </ThemeIcon>
        <Title order={2}>Användare</Title>
      </Group>
      <Text c="dimmed" size="sm">
        Endast personer i den här listan kan logga in. Lämna lösenordet tomt om personen ska logga in med Google.
        Administratörer kan hantera användare och renoveringstyper.
      </Text>

      <Card withBorder padding="md">
        <form onSubmit={createForm.onSubmit(handleCreate)}>
          <Group align="flex-end">
            <TextInput label="E-post" type="email" required w={240} {...createForm.getInputProps('email')} />
            <TextInput label="Namn" required w={160} {...createForm.getInputProps('displayName')} />
            <PasswordInput
              label="Lösenord (valfritt)"
              placeholder="Endast Google"
              w={180}
              {...createForm.getInputProps('initialPassword')}
            />
            <Button type="submit" loading={createUser.isPending}>
              Lägg till
            </Button>
          </Group>
        </form>
      </Card>

      <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
        <Table striped highlightOnHover verticalSpacing="sm">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Namn</Table.Th>
              <Table.Th>E-post</Table.Th>
              <Table.Th>Inloggning</Table.Th>
              <Table.Th>Admin</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {users?.map((user) => (
              <Table.Tr key={user.id}>
                <Table.Td>
                  {user.displayName}
                  {user.id === currentUser?.id && (
                    <Badge ml="xs" size="sm" variant="light">
                      Du
                    </Badge>
                  )}
                </Table.Td>
                <Table.Td c="dimmed">{user.email}</Table.Td>
                <Table.Td>
                  <Badge variant="light" color={user.hasPassword ? 'blue' : 'grape'}>
                    {user.hasPassword ? 'Lösenord + Google' : 'Endast Google'}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  {/* Your own switch is disabled: the API returns 409 for self-demotion, which is
                      what guarantees at least one admin always exists. */}
                  <Switch
                    checked={user.isAdmin}
                    disabled={user.id === currentUser?.id}
                    onChange={() => handleToggleAdmin(user)}
                    aria-label={`Admin: ${user.displayName}`}
                  />
                </Table.Td>
                <Table.Td>
                  <ActionIcon variant="subtle" onClick={() => startEditing(user)} mr="xs">
                    <IconEdit size={16} />
                  </ActionIcon>
                  <ActionIcon
                    color="red"
                    variant="subtle"
                    disabled={user.id === currentUser?.id}
                    onClick={() => setPendingDeleteId(user.id)}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Card>

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera användare" centered>
        <form onSubmit={editForm.onSubmit(handleUpdate)}>
          <Stack>
            <TextInput label="Namn" required {...editForm.getInputProps('displayName')} />
            <Group justify="flex-end">
              <Button type="submit" loading={updateUser.isPending}>
                Spara
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Ta bort användare"
        message="Personen kommer inte längre kunna logga in. Detta kan inte ångras."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            handleDelete(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />
    </Stack>
  )
}
