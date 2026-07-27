// Swedish display labels for backend enum values. The enum values themselves (the API contract)
// stay in English — only what the user sees is translated here.
//
// Renovation types are no longer an enum (see RenovationTypesPage) — their Swedish names are
// admin-managed data now, not hardcoded here.
import type { DocumentCategory } from '../api/types'

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
