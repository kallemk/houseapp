// Swedish display labels for backend enum values. The enum values themselves (the API contract)
// stay in English — only what the user sees is translated here.
import type { DocumentCategory, RenovationCategory } from '../api/types'

export const RENOVATION_CATEGORY_LABELS: Record<RenovationCategory, string> = {
  Renovation: 'Renovering',
  Maintenance: 'Underhåll',
  Furniture: 'Möbler',
  Other: 'Övrigt',
}

export const RENOVATION_CATEGORY_OPTIONS = (Object.keys(RENOVATION_CATEGORY_LABELS) as RenovationCategory[]).map(
  (value) => ({ value, label: RENOVATION_CATEGORY_LABELS[value] }),
)

export const DOCUMENT_CATEGORY_LABELS: Record<DocumentCategory, string> = {
  Deed: 'Lagfart',
  Warranty: 'Garanti',
  Receipt: 'Kvitto',
  Photo: 'Foto',
  Other: 'Övrigt',
}

export const DOCUMENT_CATEGORY_OPTIONS = (Object.keys(DOCUMENT_CATEGORY_LABELS) as DocumentCategory[]).map((value) => ({
  value,
  label: DOCUMENT_CATEGORY_LABELS[value],
}))
