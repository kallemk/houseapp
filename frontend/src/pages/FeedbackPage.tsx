import {
  Alert,
  Badge,
  Button,
  Card,
  Center,
  Container,
  Group,
  Loader,
  Stack,
  Text,
  Textarea,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconArrowLeft, IconBulb, IconLock } from '@tabler/icons-react'
import { Link } from 'react-router-dom'
import { ApiError } from '../api/client'
import { EmptyState } from '../components/common/EmptyState'
import { AppFooter } from '../components/layout/AppFooter'
import { useCreateFeedback, useFeedback } from '../hooks/useFeedback'
import { FEEDBACK_STATUS_COLORS, FEEDBACK_STATUS_LABELS } from '../utils/labels'

interface FeedbackFormValues {
  title: string
  body: string
}

export function FeedbackPage() {
  const { data: items, isLoading, error } = useFeedback()
  const createFeedback = useCreateFeedback()

  const form = useForm<FeedbackFormValues>({
    initialValues: { title: '', body: '' },
    validate: {
      title: (value) => (value.trim().length === 0 ? 'Rubrik krävs' : null),
      body: (value) => (value.trim().length === 0 ? 'Beskrivning krävs' : null),
    },
  })

  function handleSubmit(values: FeedbackFormValues) {
    createFeedback.mutate(
      { title: values.title.trim(), body: values.body.trim() },
      {
        onSuccess: () => {
          form.reset()
          notifications.show({ color: 'green', message: 'Tack! Ditt förslag är inskickat.' })
        },
        onError: (err) =>
          notifications.show({
            color: 'red',
            message:
              err instanceof ApiError && err.status === 429
                ? 'Du har skickat in många förslag idag. Försök igen imorgon.'
                : 'Kunde inte skicka in förslaget. Försök igen.',
          }),
      },
    )
  }

  // 503 means no token is configured on the server — a deployment state, not something the user did.
  const notConfigured = error instanceof ApiError && error.status === 503

  return (
    <>
      <Container size="sm" py="xl">
        <Stack>
          <BackLink />
          <Group gap="sm">
            <ThemeIcon variant="light" size={36} radius="md">
              <IconBulb size={20} />
            </ThemeIcon>
            <Title order={2}>Förslag &amp; feedback</Title>
          </Group>
          <Text c="dimmed" size="sm">
            Saknar du något, eller har något gått sönder? Skriv här så hamnar det direkt hos den som
            bygger appen. Varje förslag får en <strong>status</strong> som visar vad som händer med
            det, och märks <strong>Öppet</strong> eller <strong>Stängt</strong> beroende på om det
            fortfarande är under arbete.
          </Text>

          {notConfigured ? (
            <Alert variant="light" color="gray" icon={<IconLock size={18} />}>
              Förslagsfunktionen är inte påslagen just nu. Försök igen senare.
            </Alert>
          ) : (
            <>
              <Card withBorder padding="md">
                <form onSubmit={form.onSubmit(handleSubmit)}>
                  <Stack>
                    <TextInput
                      label="Rubrik"
                      placeholder="t.ex. Exportera projekt till Excel"
                      required
                      {...form.getInputProps('title')}
                    />
                    <Textarea
                      label="Beskrivning"
                      placeholder="Beskriv gärna vad du vill kunna göra, och varför."
                      autosize
                      minRows={4}
                      required
                      {...form.getInputProps('body')}
                    />
                    {/* Said plainly rather than buried: this text leaves the app, and the owner may
                        choose to show it to other användare. */}
                    <Text size="xs" c="dimmed">
                      Ditt namn skickas med så att vi vet vem som föreslagit vad — men inte din
                      e-postadress. Skriv inte in personuppgifter eller annat känsligt. Förslaget kan
                      komma att visas för andra användare.
                    </Text>
                    <Group justify="flex-end">
                      <Button type="submit" loading={createFeedback.isPending}>
                        Skicka in
                      </Button>
                    </Group>
                  </Stack>
                </form>
              </Card>

              {isLoading ? (
                <Center py="xl">
                  <Loader />
                </Center>
              ) : !items || items.length === 0 ? (
                <EmptyState icon={IconBulb} message="Inga förslag ännu. Ditt kan bli det första." />
              ) : (
                <Stack gap="sm">
                  {items.map((item) => (
                    <Card key={item.number} withBorder padding="md">
                      <Group justify="space-between" align="flex-start" wrap="nowrap" mb={4}>
                        <Text fw={600}>{item.title}</Text>
                        <Group gap={6} wrap="nowrap">
                          {item.isMine && (
                            <Badge size="sm" variant="light">
                              Ditt
                            </Badge>
                          )}
                          {/* Only ever seen by its submitter or an admin, so saying so is useful
                              rather than confusing. */}
                          {!item.isPublished && (
                            <Badge size="sm" variant="outline" color="gray">
                              Inte publicerat
                            </Badge>
                          )}
                          <Badge size="sm" variant="light" color={FEEDBACK_STATUS_COLORS[item.status]}>
                            {FEEDBACK_STATUS_LABELS[item.status]}
                          </Badge>
                          {/* Shown separately from the status: a status label overrides the derived
                              status, so a closed suggestion labelled "pågår" would otherwise look
                              like it was still being worked on. */}
                          <Badge size="sm" variant="outline" color={item.isOpen ? 'blue' : 'gray'}>
                            {item.isOpen ? 'Öppet' : 'Stängt'}
                          </Badge>
                        </Group>
                      </Group>
                      <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                        {item.body}
                      </Text>
                      {item.reply && (
                        <Alert
                          variant="light"
                          color="blue"
                          mt="sm"
                          title={`Svar från ${item.reply.author} · ${item.reply.createdAt.slice(0, 10)}`}
                        >
                          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                            {item.reply.body}
                          </Text>
                        </Alert>
                      )}
                      <Text size="xs" c="dimmed" mt="xs">
                        Inskickat {item.createdAt.slice(0, 10)}
                      </Text>
                    </Card>
                  ))}
                </Stack>
              )}
            </>
          )}
        </Stack>
      </Container>
      <AppFooter />
    </>
  )
}

/** Back into the app — this page is reached from the footer, which is on every screen. */
function BackLink() {
  return (
    <Text
      component={Link}
      to="/"
      size="sm"
      c="terracotta"
      style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}
    >
      <IconArrowLeft size={14} />
      Tillbaka
    </Text>
  )
}
