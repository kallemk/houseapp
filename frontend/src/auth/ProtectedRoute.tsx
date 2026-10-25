import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { FullPageLoader } from '../components/common/FullPageLoader'
import { useAuth } from './AuthContext'

export function ProtectedRoute() {
  const { user, loading } = useAuth()
  const location = useLocation()

  // The first request of the day pays for the App Service's cold start, and this is where the wait
  // lands — so the loader here is the one that explains itself.
  if (loading) {
    return <FullPageLoader />
  }

  if (!user) {
    // Remember where they were going. LoginPage has always honoured state.from; nothing set it until
    // Claude's consent page needed to survive a sign-in in the middle of connecting.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return <Outlet />
}
