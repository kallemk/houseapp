import { Anchor, Card, Container, Group, List, Stack, Table, Text, ThemeIcon, Title } from '@mantine/core'
import { IconArrowLeft, IconCookie } from '@tabler/icons-react'
import { Link } from 'react-router-dom'
import { AppFooter, CONTACT_EMAIL } from '../components/layout/AppFooter'

/**
 * Public on purpose — reachable without signing in, since deciding whether to sign in is exactly
 * when someone would want to read it.
 *
 * There is deliberately **no cookie consent banner**. The app sets one cookie, and it exists only to
 * keep you signed in; strictly necessary storage of that kind doesn't require consent. A banner here
 * would ask permission for something the app can't work without, which teaches people to dismiss
 * consent dialogs without reading them. If anything is ever added that *isn't* necessary — analytics,
 * embedded media, advertising — that changes, and this page is where you'd notice.
 *
 * Keep this page honest rather than exhaustive: if the storage below changes, change this text.
 */
export function CookiesPage() {
  return (
    <>
      <Container size="sm" py="xl">
        <Stack>
          <Anchor component={Link} to="/" size="sm">
            <Group gap={4}>
              <IconArrowLeft size={14} />
              Tillbaka
            </Group>
          </Anchor>

          <Group gap="sm">
            <ThemeIcon variant="light" size={36} radius="md">
              <IconCookie size={20} />
            </ThemeIcon>
            <Title order={2}>Cookies &amp; data</Title>
          </Group>

          <Text c="dimmed" size="sm">
            HusTracker drivs av Odenbulten Consulting AB (org.nr 559289-6285). Den här sidan beskriver
            exakt vad appen sparar i din webbläsare och vad som lagras om dig — inget mer, inget mindre.
          </Text>

          <Title order={4} mt="md">
            Ingen cookie-banner — och varför
          </Title>
          <Text size="sm">
            Appen använder <strong>en enda cookie</strong>, och den finns bara för att hålla dig
            inloggad. Sådan nödvändig lagring kräver inget samtycke, så det finns ingen banner att
            klicka bort. Vi använder inga cookies för analys, statistik, spårning eller annonser — och
            delar inga uppgifter med tredje part för sådana ändamål.
          </Text>

          <Title order={4} mt="md">
            Det här sparas i din webbläsare
          </Title>
          <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
            <Table.ScrollContainer minWidth={560}>
              <Table verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Namn</Table.Th>
                    <Table.Th>Typ</Table.Th>
                    <Table.Th>Syfte</Table.Th>
                    <Table.Th>Livslängd</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  <Table.Tr>
                    <Table.Td>
                      <Text size="sm" ff="monospace">
                        houseapp.auth
                      </Text>
                    </Table.Td>
                    <Table.Td>Cookie</Table.Td>
                    <Table.Td>
                      Håller dig inloggad. Innehållet är krypterat och kan bara läsas av servern.
                      Cookien är <em>HttpOnly</em> (oåtkomlig för skript i webbläsaren) och skickas
                      bara till den här webbplatsen.
                    </Table.Td>
                    <Table.Td>
                      14 dagar, förlängs medan du är aktiv. Försvinner när du loggar ut.
                    </Table.Td>
                  </Table.Tr>
                  <Table.Tr>
                    <Table.Td>
                      <Text size="sm" ff="monospace">
                        houseapp:lastPropertyId
                      </Text>
                    </Table.Td>
                    <Table.Td>Local storage</Table.Td>
                    <Table.Td>
                      Kommer ihåg vilken bostad du senast tittade på, så appen öppnar där i stället för
                      på listan. Innehåller bara ett id.
                    </Table.Td>
                    <Table.Td>Tills du rensar webbläsardata</Table.Td>
                  </Table.Tr>
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Card>

          <Title order={4} mt="md">
            Inloggning med Google
          </Title>
          <Text size="sm">
            Väljer du att logga in med Google laddas Googles inloggningsskript på inloggningssidan, och
            Google kan då sätta egna cookies under sina domäner. De cookiesarna är Googles, inte våra,
            och styrs av{' '}
            <Anchor href="https://policies.google.com/privacy" target="_blank" rel="noreferrer">
              Googles integritetspolicy
            </Anchor>
            . Appen tar bara emot din e-postadress, ditt namn och ett id från Google — aldrig ditt
            lösenord. Du kan även logga in med e-post och lösenord i stället.
          </Text>

          <Title order={4} mt="md">
            Vad som lagras om dig i appen
          </Title>
          <List size="sm" spacing="xs">
            <List.Item>
              <strong>Ditt konto:</strong> e-postadress, namn och (om du valt lösenord) en krypterad
              version av lösenordet. Aldrig lösenordet i klartext.
            </List.Item>
            <List.Item>
              <strong>Det du själv lägger in:</strong> bostäder, värderingar, projekt, kostnader,
              budgetar och dokument.
            </List.Item>
            <List.Item>
              <strong>Dina dokument</strong> lagras antingen i appens lagring eller — om du kopplat
              det — i din egen Google Drive. Väljer du Drive är filerna dina och ligger kvar där även
              om du kopplar från.
            </List.Item>
          </List>

          <Title order={4} mt="md">
            Vem kan se det
          </Title>
          <Text size="sm">
            En bostad är privat för sina medlemmar. Ingen ser dina uppgifter förrän du delar bostaden
            med någon, och du kan ta bort delningen igen. Undantaget är <strong>demobostaden</strong>,
            som är en gemensam sandlåda — allt som läggs in där är synligt för alla inloggade, så lägg
            inte in något privat i den.
          </Text>

          <Title order={4} mt="md">
            Om du skickar in ett förslag
          </Title>
          <Text size="sm">
            Skickar du in ett förslag via <strong>Förslag &amp; feedback</strong> sparas det som ett
            ärende i appens privata kodförråd hos GitHub — alltså utanför den lagring som beskrivs
            ovan. Det som skickas med är din text, ditt namn och ditt användar-id.{' '}
            <strong>Din e-postadress skickas aldrig dit.</strong> Förslag är privata tills vi väljer
            att publicera dem i appen, och först då kan andra användare se dem. Skriv därför inte in
            personuppgifter eller annat känsligt i ett förslag.
          </Text>

          <Title order={4} mt="md">
            Var det ligger, och hur du blir av med det
          </Title>
          <Text size="sm">
            Uppgifterna lagras i Microsoft Azure inom EU (undantaget förslag, se ovan). Vill du radera
            ditt konto och dina uppgifter, eller få veta vad som finns lagrat om dig, mejla{' '}
            <Anchor href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</Anchor> så ordnar vi det.
          </Text>

          <Text size="xs" c="dimmed" mt="md">
            Sidan beskriver hur appen faktiskt fungerar och är inte juridisk rådgivning.
          </Text>
        </Stack>
      </Container>
      <AppFooter />
    </>
  )
}
