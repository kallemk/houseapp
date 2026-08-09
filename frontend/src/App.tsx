import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes, useParams } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AppLayout } from './components/layout/AppLayout'
import { CookiesPage } from './pages/CookiesPage'
import { FeedbackPage } from './pages/FeedbackPage'
import { AdministrationPage } from './pages/AdministrationPage'
import { DashboardPage } from './pages/DashboardPage'
import { DocumentsPage } from './pages/DocumentsPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { BudgetPage } from './pages/BudgetPage'
import { FinancesPage } from './pages/FinancesPage'
import { MaintenancePage } from './pages/MaintenancePage'
import { MaintenanceSectionPage } from './pages/MaintenanceSectionPage'
import { ProjectDetailPage } from './pages/ProjectDetailPage'
import { ProjectsPage } from './pages/ProjectsPage'
import { PropertyComponentsPage } from './pages/PropertyComponentsPage'
import { PropertyLocalComponentsPage } from './pages/PropertyLocalComponentsPage'
import { PropertyPickerPage } from './pages/PropertyPickerPage'
import { SpendingPage } from './pages/SpendingPage'
import { UpcomingPage } from './pages/UpcomingPage'
import { UsersPage } from './pages/UsersPage'
import { ValuationsPage } from './pages/ValuationsPage'
import { theme } from './theme'
import { getLastPropertyId } from './utils/lastProperty'

const queryClient = new QueryClient()

/** Jumps straight back into the last-viewed property, or the picker if there isn't one. The
 * target route re-validates membership itself, so a stale id here just bounces to the picker. */
function RootRedirect() {
  const lastPropertyId = getLastPropertyId()
  return <Navigate to={lastPropertyId ? `/properties/${lastPropertyId}` : '/properties'} replace />
}

/**
 * Keeps a pre-restructure URL working by sending it to wherever that page lives now. These are
 * bookmarked and were linked from inside the app for months, so they can't simply 404.
 */
function PropertyRedirect({ to }: { to: string }) {
  const { propertyId } = useParams<{ propertyId: string }>()
  return <Navigate to={`/properties/${propertyId}/${to}`} replace />
}

export default function App() {
  return (
    <MantineProvider theme={theme}>
      <Notifications />
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              {/* Public on purpose: deciding whether to sign in is exactly when someone would
                  want to read what the app stores. */}
              <Route path="/cookies" element={<CookiesPage />} />
              <Route element={<ProtectedRoute />}>
                <Route index element={<RootRedirect />} />
                <Route path="properties" element={<PropertyPickerPage />} />
                <Route path="feedback" element={<FeedbackPage />} />
                {/* Not property-scoped, and no longer pretending to be: the central registry and
                    the user list belong to the app, not to a house. Open to everyone — each page
                    under it decides what a non-admin may do. */}
                <Route path="admin" element={<AdministrationPage />}>
                  <Route index element={<Navigate to="components" replace />} />
                  <Route path="components" element={<PropertyComponentsPage />} />
                  <Route path="users" element={<UsersPage />} />
                </Route>
                <Route path="properties/:propertyId" element={<AppLayout />}>
                  <Route index element={<DashboardPage />} />
                  <Route path="projects" element={<ProjectsPage />} />
                  {/* "new" is handled by the same page in create mode. */}
                  <Route path="projects/:projectId" element={<ProjectDetailPage />} />
                  {/* The schedule and the component list it's computed from. "components" here is
                      this property's own list, not the central registry under /admin. */}
                  <Route path="maintenance" element={<MaintenanceSectionPage />}>
                    <Route index element={<Navigate to="schedule" replace />} />
                    <Route path="schedule" element={<MaintenancePage />} />
                    <Route path="components" element={<PropertyLocalComponentsPage />} />
                  </Route>
                  <Route path="finances" element={<FinancesPage />}>
                    <Route index element={<Navigate to="budget" replace />} />
                    <Route path="budget" element={<BudgetPage />} />
                    <Route path="spending" element={<SpendingPage />} />
                    <Route path="upcoming" element={<UpcomingPage />} />
                    <Route path="valuations" element={<ValuationsPage />} />
                  </Route>
                  <Route path="documents" element={<DocumentsPage />} />

                  {/* Pre-restructure URLs. */}
                  <Route path="valuations" element={<PropertyRedirect to="finances/valuations" />} />
                  <Route path="budget" element={<PropertyRedirect to="finances/budget" />} />
                  <Route path="components" element={<PropertyRedirect to="maintenance/components" />} />
                  <Route path="admin" element={<Navigate to="/admin" replace />} />
                  <Route path="admin/components" element={<Navigate to="/admin/components" replace />} />
                  <Route path="admin/users" element={<Navigate to="/admin/users" replace />} />
                </Route>
              </Route>
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </QueryClientProvider>
    </MantineProvider>
  )
}
