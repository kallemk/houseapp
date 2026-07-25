import { Alert, Button, Center, Paper, PasswordInput, Stack, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

interface LoginFormValues {
  email: string
  password: string
}

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const form = useForm<LoginFormValues>({
    initialValues: { email: '', password: '' },
    validate: {
      email: (value) => (value.length === 0 ? 'Email is required' : null),
      password: (value) => (value.length === 0 ? 'Password is required' : null),
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
      setError('Incorrect email or password.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Center h="100vh">
      <Paper withBorder shadow="sm" p="xl" w={360}>
        <Title order={3} mb="lg">
          HouseApp
        </Title>
        <form onSubmit={form.onSubmit(handleSubmit)}>
          <Stack>
            {error && (
              <Alert color="red" variant="light">
                {error}
              </Alert>
            )}
            <TextInput label="Email" type="email" required {...form.getInputProps('email')} />
            <PasswordInput label="Password" required {...form.getInputProps('password')} />
            <Button type="submit" loading={submitting} fullWidth>
              Log in
            </Button>
          </Stack>
        </form>
      </Paper>
    </Center>
  )
}
