import { Center, Loader, Stack, Text, Transition } from '@mantine/core'
import { useEffect, useState } from 'react'

/** Long enough that a warm backend never shows the message at all. */
const EXPLAIN_AFTER_MS = 2500

/** By here it's not a blip, and a second nudge stops it feeling stuck. */
const REASSURE_AFTER_MS = 12000

/**
 * The first-load spinner, with an explanation that appears only once the wait is genuinely long.
 *
 * The App Service runs on the free F1 tier with `alwaysOn: false`, so it unloads when nobody has
 * visited for a while and the next request pays for a cold start — tens of seconds, occasionally.
 * That's a deliberate trade (see the infra notes in CLAUDE.md), but from the outside it just looks
 * broken, and a bare spinner invites a refresh that makes it worse.
 *
 * The delay matters: saying "this might take a while" on a load that took 300ms would be noise, and
 * would make the app feel slower than it is. Nothing is shown unless it's earned.
 */
export function FullPageLoader() {
  const [stage, setStage] = useState<0 | 1 | 2>(0)

  useEffect(() => {
    const explain = setTimeout(() => setStage(1), EXPLAIN_AFTER_MS)
    const reassure = setTimeout(() => setStage(2), REASSURE_AFTER_MS)
    return () => {
      clearTimeout(explain)
      clearTimeout(reassure)
    }
  }, [])

  return (
    <Center h="100vh" px="md">
      <Stack align="center" gap="xs">
        <Loader />
        <Transition mounted={stage >= 1} transition="fade" duration={400}>
          {(styles) => (
            <Stack align="center" gap={4} style={styles} maw={380} mt="md">
              <Text fw={600} size="sm">
                Väcker servern …
              </Text>
              <Text size="sm" c="dimmed" ta="center">
                Den bor på billigaste möjliga abonnemang och somnar när ingen har tittat förbi på ett
                tag. Första besöket tar därför en stund.
              </Text>
              {stage >= 2 && (
                <Text size="sm" c="dimmed" ta="center" mt={4}>
                  Fortfarande på gång — den vaknar, den är bara lite morgontrött.
                </Text>
              )}
            </Stack>
          )}
        </Transition>
      </Stack>
    </Center>
  )
}
