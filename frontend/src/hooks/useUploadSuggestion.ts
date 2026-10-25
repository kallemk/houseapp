import { useSearchParams } from 'react-router-dom'
import type { DocumentCategory } from '../api/types'
import { DOCUMENT_CATEGORY_LABELS } from '../utils/labels'

export interface UploadSuggestion {
  title?: string
  category?: DocumentCategory
  date?: string
}

/**
 * Reads the suggestions in a link Claude made with `create_upload_link`:
 * `?upload=1&title=…&category=…&date=…`. Claude can't send files itself — it never sees the original
 * of something attached in a chat — so it sends the user here, to the ordinary upload, with what it
 * already knows filled in.
 *
 * Everything is checked rather than trusted: it's a URL anyone could have written. An unknown
 * category or a malformed date is dropped, not passed on. `clear` removes the parameters once the
 * upload is done, so a reload doesn't pre-fill the next one.
 */
export function useUploadSuggestion(): { suggestion: UploadSuggestion | null; clear: () => void } {
  const [params, setParams] = useSearchParams()

  const clear = () =>
    setParams(
      (current) => {
        for (const key of ['upload', 'title', 'category', 'date']) current.delete(key)
        return current
      },
      { replace: true },
    )

  if (params.get('upload') !== '1') {
    return { suggestion: null, clear }
  }

  const title = params.get('title')?.trim().slice(0, 200) || undefined
  const category = params.get('category')
  const date = params.get('date')

  return {
    suggestion: {
      title,
      category: category && category in DOCUMENT_CATEGORY_LABELS ? (category as DocumentCategory) : undefined,
      date: date && /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : undefined,
    },
    clear,
  }
}
