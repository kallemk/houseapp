import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AppLayout } from './components/layout/AppLayout'
import { AdministrationPage } from './pages/AdministrationPage'
import { DashboardPage } from './pages/DashboardPage'
import { DocumentsPage } from './pages/DocumentsPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { BudgetPage } from './pages/BudgetPage'
import { MaintenancePage } from './pages/MaintenancePage'
import { ProjectDetailPage } from './pages/ProjectDetailPage'
import { ProjectsPage } from './pages/ProjectsPage'
import { PropertyComponentsPage } from './pages/PropertyComponentsPage'
import { PropertyPickerPage } from './pages/PropertyPickerPage'
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

export default function App() {
  return (
    <MantineProvider theme={theme}>
      <Notifications />
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route index element={<RootRedirect />} />
                <Route path="properties" element={<PropertyPickerPage />} />
                <Route path="properties/:propertyId" element={<AppLayout />}>
                  <Route index element={<DashboardPage />} />
                  <Route path="valuations" element={<ValuationsPage />} />
                  <Route path="projects" element={<ProjectsPage />} />
                  {/* "new" is handled by the same page in create mode. */}
                  <Route path="projects/:projectId" element={<ProjectDetailPage />} />
                  <Route path="maintenance" element={<MaintenancePage />} />
                  <Route path="budget" element={<BudgetPage />} />
                  <Route path="documents" element={<DocumentsPage />} />
                  {/* Open to everyone — each page under it decides what a non-admin may do. */}
                  <Route path="admin" element={<AdministrationPage />}>
                    <Route index element={<Navigate to="components" replace />} />
                    <Route path="components" element={<PropertyComponentsPage />} />
                    <Route path="users" element={<UsersPage />} />
                  </Route>
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
