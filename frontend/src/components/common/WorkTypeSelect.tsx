import { Select, Stack, Text, type ComboboxLikeRenderOptionInput, type ComboboxItem, type SelectProps } from '@mantine/core'
import type { WorkType } from '../../api/types'
import { WORK_TYPE_DESCRIPTIONS, WORK_TYPE_OPTIONS } from '../../utils/labels'

function descriptionFor(value: string | null | undefined): string | undefined {
  return value ? WORK_TYPE_DESCRIPTIONS[value as WorkType] : undefined
}

/**
 * The work-type picker, with each type's meaning spelled out.
 *
 * The four are genuinely easy to confuse — especially renovating versus investing — and the choice
 * isn't cosmetic: it decides which dashboard figure the money lands in, whether it counts toward
 * "Mot insatt kapital", and whether it resets a component's maintenance clock. So the distinction is
 * shown in the dropdown rather than left to be guessed, and the chosen one stays visible underneath.
 *
 * `data` is overridable for the projects page's filter, which prepends an "Alla" option that has no
 * description — hence the lookup tolerating a value it doesn't recognise.
 */
export function WorkTypeSelect({ data = WORK_TYPE_OPTIONS, ...props }: SelectProps) {
  return (
    <Select
      data={data}
      description={descriptionFor(props.value)}
      renderOption={({ option }: ComboboxLikeRenderOptionInput<ComboboxItem>) => {
        const description = descriptionFor(option.value)
        return (
          <Stack gap={0}>
            <Text size="sm">{option.label}</Text>
            {description && (
              <Text size="xs" c="dimmed">
                {description}
              </Text>
            )}
          </Stack>
        )
      }}
      {...props}
    />
  )
}
