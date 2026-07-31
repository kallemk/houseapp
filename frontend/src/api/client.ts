export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/**
 * Statuses that mean "the backend isn't up yet", not "here is your answer".
 *
 * The App Service is F1 with alwaysOn disabled, so it unloads after ~20 minutes idle and the next
 * request pays a cold start of tens of seconds — long enough that the proxy in front of it gives up
 * first. Retrying is what turns that into a pause instead of a failure.
 *
 * 401/403/404/409 are deliberately absent: those are real answers and retrying them would only slow
 * down telling the user the truth. 500 is absent too — a genuine server bug should surface, not be
 * papered over. The one false positive is /api/auth/google's 503 for "Google sign-in isn't
 * configured", which is permanent; it costs a few seconds of pointless retrying before showing the
 * same message, which is a fair price for covering the real case.
 */
const WAKING_STATUSES = [502, 503, 504]

/** Attempt 1 fails → wait 1s, then 3s, then 6s. Four attempts in total. */
const RETRY_DELAYS_MS = [1000, 3000, 6000]

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

interface RequestOptions {
  /**
   * Whether this call is safe to repeat. Defaults to true for GET and false for everything else,
   * because a POST that times out may still have been processed — retrying a Drive upload would
   * quietly create the file twice.
   */
  retryWhileWaking?: boolean
}

async function request<T>(path: string, init?: RequestInit, options?: RequestOptions): Promise<T> {
  // FormData must go out *without* a Content-Type of ours: the browser sets it itself so it can
  // append the multipart boundary, and forcing application/json makes the body unparseable server-side.
  const isFormData = init?.body instanceof FormData
  const retry = options?.retryWhileWaking ?? (init?.method ?? 'GET') === 'GET'

  for (let attempt = 0; ; attempt++) {
    const canRetry = retry && attempt < RETRY_DELAYS_MS.length

    let response: Response
    try {
      response = await fetch(`/api${path}`, {
        credentials: 'include',
        headers: isFormData ? { ...init?.headers } : { 'Content-Type': 'application/json', ...init?.headers },
        ...init,
      })
    } catch (networkError) {
      // fetch only rejects when the request never got an answer — exactly the sleeping-backend case.
      if (canRetry) {
        await sleep(RETRY_DELAYS_MS[attempt])
        continue
      }
      throw networkError
    }

    if (!response.ok) {
      if (canRetry && WAKING_STATUSES.includes(response.status)) {
        await sleep(RETRY_DELAYS_MS[attempt])
        continue
      }

      const message = await response.text().catch(() => response.statusText)
      throw new ApiError(response.status, message || response.statusText)
    }

    if (response.status === 204) {
      return undefined as T
    }

    return (await response.json()) as T
  }
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }, options),
  /** Multipart, for the one endpoint that takes a file (Drive uploads). Never retried. */
  postForm: <T>(path: string, form: FormData) => request<T>(path, { method: 'POST', body: form }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
