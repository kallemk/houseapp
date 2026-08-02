import { Anchor, Container, Group, Stack, Text } from '@mantine/core'
import { IconBrandGithub } from '@tabler/icons-react'
import { Link } from 'react-router-dom'

const ODENBULTEN_URL =
  'https://www.allabolag.se/foretag/odenbulten-consulting-ab/k%C3%A5llered/konsulter/2KHV6E5I5YF3I'

const REPO_URL = 'https://github.com/kallemk/houseapp/blob/main/PITCH.md'

/** Also the address the cookies page points at for data and deletion requests — keep the two in step. */
export const CONTACT_EMAIL = 'info@odenbulten.se'

const ORG_NUMBER = '559289-6285'

/**
 * Injected at build time from the commit sha (see ci-cd.yml). Empty locally, where a build number
 * would be meaningless — the point of showing it is being able to ask "which version are you on?"
 * when something looks wrong in production.
 */
const VERSION = import.meta.env.VITE_APP_VERSION as string | undefined

export function AppFooter() {
  return (
    <Container size="lg" py="lg">
      <Group justify="space-between" gap="sm" wrap="wrap">
        {/* Two lines rather than one: the company details and the build identifier are read for
            different reasons, and on a phone a single line of all of it wraps into a muddle. */}
        <Stack gap={2}>
          <Text size="xs" c="dimmed">
            © {new Date().getFullYear()}{' '}
            <Anchor href={ODENBULTEN_URL} target="_blank" rel="noreferrer" c="dimmed" underline="always">
              Odenbulten Consulting AB
            </Anchor>
            {' · '}
            Org.nr {ORG_NUMBER}
          </Text>
          <Text size="xs" c="dimmed">
            <Anchor href={`mailto:${CONTACT_EMAIL}`} c="dimmed" underline="always">
              {CONTACT_EMAIL}
            </Anchor>
            {VERSION && (
              <Text span size="xs" c="dimmed">
                {' · '}
                {/* Short sha only — enough to identify a build, no need for the full 40 characters. */}
                version {VERSION.slice(0, 7)}
              </Text>
            )}
          </Text>
        </Stack>

        <Group gap="lg">
          <Anchor component={Link} to="/feedback" size="xs" c="dimmed">
            Förslag &amp; feedback
          </Anchor>
          <Anchor component={Link} to="/cookies" size="xs" c="dimmed">
            Cookies &amp; data
          </Anchor>
          <Anchor href={REPO_URL} target="_blank" rel="noreferrer" size="xs" c="dimmed">
            <Group gap={4} wrap="nowrap">
              <IconBrandGithub size={14} />
              Om appen
            </Group>
          </Anchor>
        </Group>
      </Group>
    </Container>
  )
}
