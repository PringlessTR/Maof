import React, { createContext, useContext, useEffect, useState, ReactNode, useCallback, useMemo } from 'react';
import { User, login as loginService, getCurrentUser, logout as logoutService } from '../services/authService';

interface AuthContextType {
  user: User | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  loading: boolean;
  isAuthenticated: boolean;
  hasPermission: (permission: string) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
  hasAllPermissions: (permissions: string[]) => boolean;
  hasRole: (role: string) => boolean;
  hasAnyRole: (roles: string[]) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [isAuthenticated, setIsAuthenticated] = useState(false);

  const loadUser = useCallback(async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      
      if (!token) {
        setUser(null);
        setIsAuthenticated(false);
        return;
      }
      
      // Try to get the current user from the server
      const user = await getCurrentUser();
      
      if (user) {
        setUser(user);
        setIsAuthenticated(true);
      } else {
        // If no user is returned, clear the auth state
        setUser(null);
        setIsAuthenticated(false);
        localStorage.removeItem('token');
      }
    } catch (error) {
      console.error('Failed to load user', error);
      // Clear any invalid token and user data
      setUser(null);
      setIsAuthenticated(false);
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadUser();
  }, [loadUser]);

  const login = useCallback(async (username: string, password: string) => {
    try {
      console.log('AuthContext: Attempting login with username:', username);
      const data = await loginService(username, password);
      
      console.log('AuthContext: Login successful, storing token and user data');
      localStorage.setItem('token', data.token);
      console.log('AuthContext: Token stored in localStorage');
      
      setUser(data.user);
      console.log('AuthContext: User state updated:', data.user);
      
      setIsAuthenticated(true);
      console.log('AuthContext: isAuthenticated set to true');
      
    } catch (error) {
      console.error('AuthContext: Login failed', error);
      // Clear any partial auth data on failure
      localStorage.removeItem('token');
      setUser(null);
      setIsAuthenticated(false);
      throw error;
    }
  }, []);

  const logout = useCallback(() => {
    logoutService();
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  const hasPermission = useCallback((permission: string): boolean => {
    if (!user) return false;
    return user.permissions?.includes(permission) || false;
  }, [user]);

  const hasAnyPermission = useCallback((permissions: string[]): boolean => {
    if (!user) return false;
    return permissions.some(permission => user.permissions?.includes(permission));
  }, [user]);

  const hasAllPermissions = useCallback((permissions: string[]): boolean => {
    if (!user) return false;
    return permissions.every(permission => user.permissions?.includes(permission));
  }, [user]);

  const hasRole = useCallback((role: string): boolean => {
    if (!user) return false;
    return user.roles?.includes(role) || false;
  }, [user]);

  const hasAnyRole = useCallback((roles: string[]): boolean => {
    if (!user) return false;
    return roles.some(role => user.roles?.includes(role));
  }, [user]);

  const contextValue = React.useMemo(() => ({
    user,
    login,
    logout,
    loading,
    isAuthenticated,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    hasRole,
    hasAnyRole,
  }), [user, login, logout, loading, isAuthenticated, hasPermission, hasAnyPermission, hasAllPermissions, hasRole, hasAnyRole]);

  return (
    <AuthContext.Provider value={contextValue}>
      {!loading && children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
