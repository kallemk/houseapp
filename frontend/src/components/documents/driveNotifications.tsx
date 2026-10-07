import { Anchor, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { isDriveConnectionExpired } from '../../api/client'
import { driveApi } from '../../api/documents'

/**
 * Turns a failed Drive operation into a message that says what to do. A lapsed grant gets a direct
 * way to renew it — the generic "try again" this replaced sent people round in circles, since
 * retrying can never fix an expired connection.
 */
export function notifyDriveFailure(error: unknown, propertyId: string, fallback: string) {
  if (!isDriveConnectionExpired(error)) {
    notifications.show({ color: 'red', message: fallback })
    return
  }

  notifications.show({
    color: 'orange',
    title: 'Google Drive-anslutningen har gått ut',
    autoClose: false,
    message: (
      <Text size="sm">
        Den behöver förnyas innan dokument kan sparas i Drive.{' '}
        <Anchor component="button" size="sm" onClick={() => driveApi.connect(propertyId)}>
          Förnya anslutning
        </Anchor>
      </Text>
    ),
  })
}
