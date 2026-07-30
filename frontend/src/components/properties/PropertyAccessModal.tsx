import { ActionIcon, Autocomplete, Group, Loader, Modal, Stack, Text } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { notifications } from '@mantine/notifications'
import { IconUserPlus, IconX } from '@tabler/icons-react'
import { useState } from 'react'
import { ApiError } from '../../api/client'
import type { PropertyDto } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import {
  useAddPropertyMember,
  useMemberSearch,
  usePropertyMembers,
  useRemovePropertyMember,
} from '../../hooks/usePropertyMembers'

/**
 * Who has access to one property. Everyone listed here can edit the property *and* manage this list
 * — there's no owner role, so adding someone is handing them the same keys you have.
 */
export function PropertyAccessModal({
  property,
  onClose,
}: {
  property: PropertyDto | null
  onClose: () => void
}) {
  const { user } = useAuth()
  const propertyId = property?.id ?? ''
  const [search, setSearch] = useState('')
  // The server ignores anything shorter than two characters; debouncing keeps a keystroke from
  // being a request.
  const [debouncedSearch] = useDebouncedValue(search, 250)

  const { data: members, isLoading } = usePropertyMembers(propertyId, property !== null)
  const { data: candidates, isFetching } = useMemberSearch(propertyId, debouncedSearch)
  const addMember = useAddPropertyMember(propertyId)
  const removeMember = useRemovePropertyMember(propertyId)

  // Autocomplete works in strings, so label back to id on pick.
  const optionLabel = (name: string, email: string) => `${name} — ${email}`
  const options = (candidates ?? []).map((c) => optionLabel(c.displayName, c.email))

  function handlePick(value: string) {
    const picked = (candidates ?? []).find((c) => optionLabel(c.displayName, c.email) === value)
    if (!picked) {
      return
    }

    setSearch('')
    addMember.mutate(picked.userId, {
      onError: (error) =>
        notifications.show({
          color: 'red',
          message:
            error instanceof ApiError && error.status === 409
              ? 'Personen har redan åtkomst.'
              : 'Kunde inte lägga till personen. Försök igen.',
        }),
    })
  }

  function handleRemove(userId: string) {
    removeMember.mutate(userId, {
      onError: (error) =>
        notifications.show({
          color: 'red',
          message:
            error instanceof ApiError && error.status === 409
              ? 'Bostaden måste ha minst en person med åtkomst.'
              : 'Kunde inte ta bort åtkomsten. Försök igen.',
        }),
    })
  }

  return (
    <Modal opened={property !== null} onClose={onClose} title={`Åtkomst – ${property?.nickname ?? ''}`} centered>
      <Stack>
        <Text size="sm" c="dimmed">
          Alla som listas här kan se och ändra bostaden, och kan även lägga till eller ta bort andra.
        </Text>

        {isLoading ? (
          <Loader size="sm" />
        ) : (
          <Stack gap="xs">
            {(members ?? []).map((member) => (
              <Group key={member.userId} justify="space-between" wrap="nowrap">
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" truncate>
                    {member.displayName}
                    {member.userId === user?.id && ' (du)'}
                  </Text>
                  <Text size="xs" c="dimmed" truncate>
                    {member.email}
                  </Text>
                </div>
                <ActionIcon
                  variant="subtle"
                  color="red"
                  title="Ta bort åtkomst"
                  onClick={() => handleRemove(member.userId)}
                >
                  <IconX size={16} />
                </ActionIcon>
              </Group>
            ))}
          </Stack>
        )}

        <Autocomplete
          label="Ge någon åtkomst"
          placeholder="Sök på namn eller e-post"
          description="Skriv minst två tecken. Personen måste redan ha ett konto."
          value={search}
          onChange={setSearch}
          onOptionSubmit={handlePick}
          data={options}
          leftSection={<IconUserPlus size={16} />}
          rightSection={isFetching ? <Loader size="xs" /> : null}
        />

        {/* Autocomplete has no nothingFoundMessage of its own, and silence here reads as "still
            loading" rather than "no such person". */}
        {debouncedSearch.trim().length >= 2 && !isFetching && options.length === 0 && (
          <Text size="xs" c="dimmed">
            Ingen användare hittades — be en administratör lägga till personen först.
          </Text>
        )}
      </Stack>
    </Modal>
  )
}
