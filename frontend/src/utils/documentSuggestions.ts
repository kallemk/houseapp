import type { DocumentCategory } from '../api/types'

/**
 * Starting values for the upload dialog, so naming a document is a matter of checking a suggestion
 * rather than typing blind. Both are only ever suggestions — the dialog shows them in editable
 * fields, and the title stays required, so nothing is saved that the user hasn't seen.
 */

/** "Offert_takbyte-2026.pdf" → "Offert takbyte 2026". */
export function suggestTitle(fileName: string): string {
  const withoutExtension = fileName.replace(/\.[^.]+$/, '')
  const spaced = withoutExtension.replace(/[_\-.]+/g, ' ').replace(/\s+/g, ' ').trim()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

/**
 * Matched against the filename, Swedish and English, before the file type — a photographed receipt
 * called "kvitto.jpg" is a receipt, not a photo.
 */
const KEYWORDS: [RegExp, DocumentCategory][] = [
  [/offert|quote|quotation/, 'Quote'],
  [/faktura|invoice/, 'Invoice'],
  [/kvitto|receipt/, 'Receipt'],
  [/garanti|warranty/, 'Warranty'],
  [/lagfart|deed/, 'Deed'],
]

export function suggestCategory(file: File): DocumentCategory {
  const name = file.name.toLowerCase()
  const match = KEYWORDS.find(([pattern]) => pattern.test(name))
  if (match) {
    return match[1]
  }
  return file.type.startsWith('image/') ? 'Photo' : 'Other'
}
