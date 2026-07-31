import { Navigate, Outlet } from 'react-router-dom'
import { FullPageLoader } from '../components/common/FullPageLoader'
import { useAuth } from './AuthContext'

export function ProtectedRoute() {
  const { user, loading } = useAuth()

  // The first request of the day pays for the App Service's cold start, and this is where the wait
  // lands — so the loader here is the one that explains itself.
  if (loading) {
    return <FullPageLoader />
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
