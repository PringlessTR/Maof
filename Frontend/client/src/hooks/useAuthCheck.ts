import { useCallback } from 'react';
import { useAuth } from '../context/AuthContext';

/**
 * Custom hook to check user authentication and permissions
 */
const useAuthCheck = () => {
  const {
    isAuthenticated,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    hasRole,
    hasAnyRole,
    user,
  } = useAuth();

  /**
   * Check if the current user has a specific permission
   */
  const checkPermission = useCallback(
    (permission: string): boolean => {
      return hasPermission(permission);
    },
    [hasPermission]
  );

  /**
   * Check if the current user has any of the specified permissions
   */
  const checkAnyPermission = useCallback(
    (permissions: string[]): boolean => {
      return hasAnyPermission(permissions);
    },
    [hasAnyPermission]
  );

  /**
   * Check if the current user has all of the specified permissions
   */
  const checkAllPermissions = useCallback(
    (permissions: string[]): boolean => {
      return hasAllPermissions(permissions);
    },
    [hasAllPermissions]
  );

  /**
   * Check if the current user has a specific role
   */
  const checkRole = useCallback(
    (role: string): boolean => {
      return hasRole(role);
    },
    [hasRole]
  );

  /**
   * Check if the current user has any of the specified roles
   */
  const checkAnyRole = useCallback(
    (roles: string[]): boolean => {
      return hasAnyRole(roles);
    },
    [hasAnyRole]
  );

  return {
    isAuthenticated,
    user,
    checkPermission,
    checkAnyPermission,
    checkAllPermissions,
    checkRole,
    checkAnyRole,
  };
};

export default useAuthCheck;
