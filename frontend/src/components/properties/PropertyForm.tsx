import { Button, Group, NumberInput, Select, Stack, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconMapPin } from '@tabler/icons-react'
import { useState } from 'react'
import { geocodeAddress } from '../../utils/geocode'
import { EMPTY_PROPERTY_FORM, type PropertyFormValues } from '../../utils/propertyForm'
import { PROPERTY_TYPE_OPTIONS } from '../../utils/labels'
import { PropertyMap } from './PropertyMap'

export function PropertyForm({
  initial,
  submitLabel,
  onSubmit,
  submitting,
}: {
  initial?: PropertyFormValues
  submitLabel: string
  onSubmit: (values: PropertyFormValues) => void
  submitting: boolean
}) {
  const form = useForm<PropertyFormValues>({ initialValues: initial ?? EMPTY_PROPERTY_FORM })
  const [locating, setLocating] = useState(false)

  // Both fields must be filled and numeric — a half-typed "57." is NaN, and rendering a map from it
  // would ask OSM for a bounding box of nothing.
  const latitude = Number(form.values.latitude)
  const longitude = Number(form.values.longitude)
  const mapPosition =
    form.values.latitude !== '' &&
    form.values.longitude !== '' &&
    Number.isFinite(latitude) &&
    Number.isFinite(longitude)
      ? { latitude, longitude }
      : null

  async function lookUpCoordinates() {
    const query = [form.values.address, form.values.postalCode, form.values.city, form.values.country]
      .map((part) => part.trim())
      .filter(Boolean)
      .join(', ')
    if (!query) {
      return
    }

    setLocating(true)
    try {
      const result = await geocodeAddress(query)
      if (result) {
        form.setFieldValue('latitude', result.latitude)
        form.setFieldValue('longitude', result.longitude)
        notifications.show({ color: 'green', message: `Hittade: ${result.displayName}` })
      } else {
        notifications.show({ color: 'yellow', message: 'Hittade ingen plats för adressen. Fyll i koordinaterna manuellt.' })
      }
    } catch {
      notifications.show({ color: 'red', message: 'Kunde inte slå upp adressen just nu. Fyll i koordinaterna manuellt.' })
    } finally {
      setLocating(false)
    }
  }

  return (
    <form onSubmit={form.onSubmit(onSubmit)}>
      <Stack maw={420}>
        <TextInput label="Smeknamn" required {...form.getInputProps('nickname')} />
        <TextInput label="Adress" required {...form.getInputProps('address')} />
        <TextInput label="Adressrad 2" placeholder="valfritt" {...form.getInputProps('address2')} />
        <Group grow>
          <TextInput label="Postnummer" placeholder="valfritt" {...form.getInputProps('postalCode')} />
          <TextInput label="Ort" placeholder="valfritt" {...form.getInputProps('city')} />
        </Group>
        <TextInput label="Land" placeholder="valfritt" {...form.getInputProps('country')} />
        <TextInput
          label="Fastighetsbeteckning"
          placeholder="valfritt"
          {...form.getInputProps('propertyDesignation')}
        />
        <Group grow>
          <Select
            label="Bostadstyp"
            placeholder="valfritt"
            data={PROPERTY_TYPE_OPTIONS}
            clearable
            {...form.getInputProps('type')}
          />
          <NumberInput label="Byggår" placeholder="valfritt" min={1500} max={2100} {...form.getInputProps('yearBuilt')} />
        </Group>
        <Group grow>
          <NumberInput label="Latitud" placeholder="valfritt" decimalScale={6} {...form.getInputProps('latitude')} />
          <NumberInput label="Longitud" placeholder="valfritt" decimalScale={6} {...form.getInputProps('longitude')} />
        </Group>
        {/* Fills the two fields above from the address; they stay editable and are what's stored. */}
        <Button
          variant="light"
          leftSection={<IconMapPin size={16} />}
          onClick={lookUpCoordinates}
          loading={locating}
          disabled={!form.values.address.trim()}
        >
          Hämta koordinater från adressen
        </Button>
        {/* Driven by the live form values, not the saved property: the map is here to check that the
            coordinates point at the right house before saving them — which is exactly what the
            geocode button above can get wrong. */}
        {mapPosition && (
          <PropertyMap
            latitude={mapPosition.latitude}
            longitude={mapPosition.longitude}
            label={form.values.nickname || form.values.address}
          />
        )}
        <TextInput label="Köpdatum" type="date" required {...form.getInputProps('purchaseDate')} />
        <TextInput label="Köpeskilling (kr)" type="number" required {...form.getInputProps('purchasePrice')} />
        <Button type="submit" loading={submitting}>
          {submitLabel}
        </Button>
      </Stack>
    </form>
  )
}
