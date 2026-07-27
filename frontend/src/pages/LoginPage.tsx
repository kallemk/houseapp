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
import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

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
    } catch {
      setError('Fel e-post eller lösenord.')
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
    try {
      await loginWithGoogle(credential)
      navigate('/', { replace: true })
    } catch (err) {
      // 403 means Google vouched for the account but it isn't on the allowlist — a genuinely
      // different situation from a failed sign-in, and worth saying so plainly.
      setError(
        err instanceof ApiError && err.status === 403
          ? 'Det här kontot har inte behörighet. Be någon lägga till din e-postadress.'
          : 'Google-inloggningen misslyckades. Försök igen.',
      )
    }
  }

  return (
    <Center h="100vh" style={{ background: 'linear-gradient(160deg, #fdf3f0 0%, #faf6f2 45%, #f3e8df 100%)' }}>
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
            {error && (
              <Alert color="red" variant="light">
                {error}
              </Alert>
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
      </Stack>
    </Center>
  )
}
