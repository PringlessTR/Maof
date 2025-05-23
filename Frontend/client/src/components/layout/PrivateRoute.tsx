import React, { ReactNode, ReactElement } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { CircularProgress, Box, Typography } from '@mui/material';

type PrivateRouteProps = {
  children?: ReactNode;
  roles?: string[];
  requiredPermissions?: string[];
  anyPermission?: string[];
  adminOnly?: boolean;
  storeOnly?: boolean;
  unauthorizedComponent?: ReactNode;
  loadingComponent?: ReactNode;
};



/**
 * A component that renders children only if the user is authenticated and has the required permissions/roles
 * Otherwise, it will redirect to the login page or unauthorized page
 */
const PrivateRoute = ({
  children,
  roles = [],
  requiredPermissions = [],
  anyPermission = [],
  adminOnly = false,
  storeOnly = false,
  unauthorizedComponent,
  loadingComponent,
}: PrivateRouteProps): ReactElement => {
  const { 
    user, 
    loading, 
    isAuthenticated,
    hasAnyPermission,
    hasAllPermissions,
    hasRole,
    hasAnyRole,
  } = useAuth();
  
  const location = useLocation();

  // Show loading state
  if (loading) {
    return loadingComponent ? (
      <>{loadingComponent}</>
    ) : (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  // Redirect to login if not authenticated
  if (!isAuthenticated || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }


  // Check admin access
  if (adminOnly && !hasRole('admin')) {
    if (unauthorizedComponent) {
      return <>{unauthorizedComponent}</> as React.ReactElement;
    }
    return <Navigate to="/unauthorized" state={{ from: location }} replace />;
  }

  // Check store access
  if (storeOnly && !user.storeId) {
    if (unauthorizedComponent) {
      return <>{unauthorizedComponent}</> as React.ReactElement;
    }
    return (
      <Box p={3} textAlign="center">
        <Typography variant="h6" color="error" gutterBottom>
          Mağaza Kullanıcısı Gerekli
        </Typography>
        <Typography variant="body1" paragraph>
          Bu sayfaya erişmek için bir mağazaya atanmış olmanız gerekmektedir.
        </Typography>
      </Box>
    );
  }

  // Check roles
  if (roles.length > 0 && !hasAnyRole(roles)) {
    if (unauthorizedComponent) {
      return <>{unauthorizedComponent}</> as React.ReactElement;
    }
    return <Navigate to="/unauthorized" state={{ from: location }} replace />;
  }

  // Check required permissions (all must be present)
  if (requiredPermissions.length > 0 && !hasAllPermissions(requiredPermissions)) {
    if (unauthorizedComponent) {
      return <>{unauthorizedComponent}</> as React.ReactElement;
    }
    return <Navigate to="/unauthorized" state={{ from: location }} replace />;
  }

  // Check any permission (at least one must be present)
  if (anyPermission.length > 0 && !hasAnyPermission(anyPermission)) {
    if (unauthorizedComponent) {
      return <>{unauthorizedComponent}</> as React.ReactElement;
    }
    return <Navigate to="/unauthorized" state={{ from: location }} replace />;
  }

  // All checks passed, render the children or outlet
  return <>{children || <Outlet />}</>;
};

export default PrivateRoute;
