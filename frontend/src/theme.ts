import { createTheme, type MantineColorsTuple } from '@mantine/core'

// Warm terracotta/clay accent — fits a house-tracking app better than a stock blue.
const terracotta: MantineColorsTuple = [
  '#fdf3f0',
  '#fae3db',
  '#f3c5b6',
  '#eba58d',
  '#e58a6b',
  '#e07853',
  '#dd6e45',
  '#c35c37',
  '#ae502f',
  '#984324',
]

export const theme = createTheme({
  primaryColor: 'terracotta',
  colors: { terracotta },
  defaultRadius: 'md',
  fontFamily:
    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
  headings: {
    fontWeight: '700',
  },
  components: {
    Card: {
      defaultProps: {
        radius: 'lg',
      },
    },
    Paper: {
      defaultProps: {
        radius: 'lg',
      },
    },
  },
})
