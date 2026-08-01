import {
  Alert,
  Button,
  Center,
  Divider,
  Group,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { GoogleLogin, GoogleOAuthProvider } from '@react-oauth/google'
import { IconHomeStar } from '@tabler/icons-react'
import { useEffect, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { AppFooter } from '../components/layout/AppFooter'
import { EXPLAIN_AFTER_MS, WakingNotice } from '../components/common/FullPageLoader'

// Vite inlines this at build time, so it must be set during `npm run build` (see
// frontend-ci-cd.yml), not at runtime. Absent locally => the Google button is hidden and the
// password form still works, so no Google setup is needed just to run the app.
const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

interface LoginFormValues {
  email: string
  password: string
}

export function LoginPage() {
  const { user, login, loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  // Tracked separately from `submitting` so a Google sign-in doesn't spin the password button.
  const [googlePending, setGooglePending] = useState(false)
  const [waking, setWaking] = useState(false)
  const busy = submitting || googlePending

  // A sign-in that's still going after a couple of seconds is almost always a cold backend rather
  // than a slow answer, and apiClient is quietly retrying underneath. Saying so beats leaving a
  // spinner that looks like nothing happened — which is what had people clicking the Google button
  // over and over.
  useEffect(() => {
    if (!busy) {
      setWaking(false)
      return
    }
    const timer = setTimeout(() => setWaking(true), EXPLAIN_AFTER_MS)
    return () => clearTimeout(timer)
  }, [busy])

  const form = useForm<LoginFormValues>({
    initialValues: { email: '', password: '' },
    validate: {
      email: (value) => (value.length === 0 ? 'E-post krävs' : null),
      password: (value) => (value.length === 0 ? 'Lösenord krävs' : null),
    },
  })

  if (user) {
    const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'
    return <Navigate to={redirectTo} replace />
  }

  async function handleSubmit(values: LoginFormValues) {
    setError(null)
    setSubmitting(true)
    try {
      await login(values.email, values.password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 403
          ? 'Det här kontot är spärrat. Kontakta en administratör om du tror att det är fel.'
          : 'Fel e-post eller lösenord.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  async function handleGoogleSuccess(credential: string | undefined) {
    if (!credential) {
      setError('Google-inloggningen misslyckades. Försök igen.')
      return
    }
    setError(null)
    setGooglePending(true)
    try {
      await loginWithGoogle(credential)
      navigate('/', { replace: true })
    } catch (err) {
      // Three genuinely different situations, so say which one it is rather than one vague
      // "it failed": blocked (403), server not set up (503), or a real auth failure (401). 403 used
      // to mean "not on the allowlist", but signing in now creates an account for anyone — so the
      // only way to get one is a deliberately blocked account.
      const status = err instanceof ApiError ? err.status : undefined
      if (status === 403) {
        setError('Det här kontot är spärrat. Kontakta en administratör om du tror att det är fel.')
      } else if (status === 503) {
        setError('Google-inloggning är inte konfigurerad på servern. Kontakta administratören.')
      } else {
        setError('Google-inloggningen misslyckades. Försök igen.')
      }
    } finally {
      setGooglePending(false)
    }
  }

  return (
    <Center
      mih="100vh"
      py="xl"
      style={{ background: 'linear-gradient(160deg, #fdf3f0 0%, #faf6f2 45%, #f3e8df 100%)' }}
    >
      <Stack align="center" gap="lg">
        <Stack align="center" gap={4}>
          <ThemeIcon variant="light" size={56} radius="xl">
            <IconHomeStar size={30} />
          </ThemeIcon>
          <Title order={2}>HusTracker</Title>
          <Text c="dimmed" size="sm">
            Håll koll på hemmet, tillsammans
          </Text>
        </Stack>

        <Paper withBorder shadow="md" p="xl" w={360}>
          <Stack>
            {/* Never both: while a sign-in is in flight there is no failure to report yet, and the
                waking notice would otherwise sit under a stale red alert from a previous attempt. */}
            {waking ? (
              <Alert color="gray" variant="light">
                <WakingNotice />
              </Alert>
            ) : (
              error && (
                <Alert color="red" variant="light">
                  {error}
                </Alert>
              )
            )}

            {GOOGLE_CLIENT_ID && (
              <>
                {/* locale lives on the provider, not the button — it's what makes Google render
                    "Logga in med Google" rather than English. */}
                <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID} locale="sv">
                  <Group justify="center">
                    <GoogleLogin
                      onSuccess={(response) => handleGoogleSuccess(response.credential)}
                      onError={() => setError('Google-inloggningen misslyckades. Försök igen.')}
                      text="signin_with"
                      width="312"
                    />
                  </Group>
                </GoogleOAuthProvider>
                <Divider label="eller" labelPosition="center" />
              </>
            )}

            <form onSubmit={form.onSubmit(handleSubmit)}>
              <Stack>
                <TextInput label="E-post" type="email" required {...form.getInputProps('email')} />
                <PasswordInput label="Lösenord" required {...form.getInputProps('password')} />
                <Button type="submit" loading={submitting} fullWidth>
                  Logga in
                </Button>
              </Stack>
            </form>
          </Stack>
        </Paper>

        {/* Kept narrow so it sits under the card rather than spanning the whole viewport. */}
        <div style={{ width: 360 }}>
          <AppFooter />
        </div>
      </Stack>
    </Center>
  )
}
