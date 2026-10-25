// A copy of the limits in backend/src/HouseApp.Api/Limits.cs that the UI needs to know up front, so
// that a too-long title or a too-big file is stopped while you're typing or dropping it rather than
// by the server afterwards. The server is still what enforces them; keep the two in step.

export const MAX_FILE_BYTES = 25 * 1024 * 1024
export const MAX_FILE_MB = MAX_FILE_BYTES / (1024 * 1024)

/** Names, titles, addresses and other one-line fields. */
export const SHORT_TEXT = 200

/** Descriptions, notes and other free text. */
export const LONG_TEXT = 4000

/** Properties a user can be a member of. The demo doesn't count. */
export const PROPERTIES_PER_USER = 5
