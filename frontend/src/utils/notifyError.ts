import { notifications } from '@mantine/notifications'
import { isLimitError } from '../api/client'

/**
 * Shows `fallback` for a failed mutation — unless a limit stopped it. Those carry their own Swedish
 * explanation, which the QueryClient in `App.tsx` already shows for every mutation, so showing
 * "försök igen" as well would be both a duplicate and wrong: retrying won't help.
 */
export function notifyError(error: unknown, fallback: string) {
  if (isLimitError(error)) {
    return
  }
  notifications.show({ color: 'red', message: fallback })
}
