import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { AuthProvider, useAuth } from './context/AuthContext';
import PrivateRoute from './components/layout/PrivateRoute';
import LoginPage from './pages/LoginPage';
import UnauthorizedPage from './components/auth/UnauthorizedPage';
import ForbiddenPage from './components/auth/ForbiddenPage';
import { Box } from '@mui/material';

// Simple dashboard component for demonstration
const Dashboard = () => {
  const { user, logout } = useAuth();
  
  return (
    <Box p={3}>
      <h1>Welcome, {user?.firstName || user?.username}!</h1>
      <p>You have successfully logged in.</p>
      <button onClick={logout}>Sign Out</button>
    </Box>
  );
};

const theme = createTheme({
  palette: {
    primary: {
      main: '#1976d2',
    },
    secondary: {
      main: '#dc004e',
    },
    background: {
      default: '#f5f5f5',
    },
  },
  typography: {
    fontFamily: '"Roboto", "Helvetica", "Arial", sans-serif',
    h4: {
      fontWeight: 600,
    },
    h5: {
      fontWeight: 500,
    },
  },
});

const App: React.FC = () => {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AuthProvider>
        <Routes>
            {/* Public routes */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/unauthorized" element={<UnauthorizedPage />} />
            <Route path="/forbidden" element={<ForbiddenPage />} />

            {/* Protected routes */}
            <Route element={
              <PrivateRoute />
            }>
              {/* Dashboard - accessible to all authenticated users */}
              <Route path="/" element={<Dashboard />} />
              <Route path="/dashboard" element={<Dashboard />} />
              
              {/* Admin section - requires admin role */}
              <Route 
                path="/admin" 
                element={
                  <PrivateRoute 
                    adminOnly 
                    unauthorizedComponent={
                      <ForbiddenPage />
                    }
                  >
                    <div>Admin Dashboard</div>
                  </PrivateRoute>
                } 
              />

              {/* Example: Store management - requires store management permissions */}
              <Route 
                path="/stores" 
                element={
                  <PrivateRoute 
                    requiredPermissions={['store.manage']}
                    unauthorizedComponent={
                      <ForbiddenPage />
                    }
                  >
                    <div>Store Management</div>
                  </PrivateRoute>
                } 
              />

              {/* Example: Reports - requires any reporting permission */}
              <Route 
                path="/reports" 
                element={
                  <PrivateRoute 
                    anyPermission={['reports.view', 'reports.export']}
                    unauthorizedComponent={
                      <ForbiddenPage />
                    }
                  >
                    <div>Reports Dashboard</div>
                  </PrivateRoute>
                } 
              />

              {/* Example: Store-specific route */}
              <Route 
                path="/store" 
                element={
                  <PrivateRoute 
                    storeOnly
                    unauthorizedComponent={
                      <ForbiddenPage />
                    }
                  >
                    <div>My Store Dashboard</div>
                  </PrivateRoute>
                } 
              />
            </Route>

            {/* Catch all other routes */}
            <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  );
};

export default App;
