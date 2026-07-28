import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

/**
 * Nests inside ProtectedRoute, so `user` is already loaded and non-null by the time this renders.
 * Purely a redirect away from a page that would fail anyway — the API is the real gate (every
 * /api/users endpoint is admin-only and returns 403).
 */
export function AdminRoute() {
  const { user } = useAuth()

  if (!user?.isAdmin) {
    return <Navigate to="/properties" replace />
  }

  return <Outlet />
}
