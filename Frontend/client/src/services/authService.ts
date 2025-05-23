import api from './api';
import axios from 'axios';

export class AuthError extends Error {
  constructor(message: string, public code: string) {
    super(message);
    this.name = 'AuthError';
  }
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface User {
  id: number;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  storeId: number | null;
  roles: string[];
  permissions: string[];
  isActive: boolean;
  lastLogin?: string;
  createdAt: string;
  updatedAt: string;
}

// Extended user interface that includes all required fields
interface ApiUser {
  id: number;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  storeId: number | null;
  roles: string[];
  permissions: string[];
  isActive: boolean;
  // These fields will be added when converting to User
  lastLogin?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: ApiUser;
  expiresIn: number;
  refreshTokenExpiresIn: number;
}

const AUTH_STORAGE_KEY = 'maof_auth';
const TOKEN_REFRESH_THRESHOLD = 300; // 5 minutes in seconds

interface StoredAuthData {
  token: string;
  refreshToken: string;
  user: User;
  expiresAt: string;
  refreshTokenExpiresAt: string;
}

const getStoredAuth = (): StoredAuthData | null => {
  const stored = localStorage.getItem(AUTH_STORAGE_KEY);
  return stored ? JSON.parse(stored) : null;
};

const storeAuthData = (data: AuthResponse): void => {
  try {
    console.log('Storing auth data in localStorage...');
    
    // Default token expiration to 1 hour if not provided
    const expiresIn = data.expiresIn || 3600; // 1 hour in seconds
    const refreshTokenExpiresIn = data.refreshTokenExpiresIn || 86400 * 7; // 7 days in seconds
    
    // Create dates with proper error handling
    const now = new Date();
    const expiresAt = new Date(now.getTime() + expiresIn * 1000);
    const refreshTokenExpiresAt = new Date(now.getTime() + refreshTokenExpiresIn * 1000);
    
    // Validate dates
    if (isNaN(expiresAt.getTime()) || isNaN(refreshTokenExpiresAt.getTime())) {
      throw new Error('Invalid date values for token expiration');
    }
    
    const authData: StoredAuthData = {
      token: data.token,
      refreshToken: data.refreshToken || data.token, // Using the same token as refresh token if not provided
      user: data.user,
      expiresAt: expiresAt.toISOString(),
      refreshTokenExpiresAt: refreshTokenExpiresAt.toISOString(),
    };
    
    console.log('Auth data to store:', {
      ...authData,
      user: '[User data]', // Don't log the entire user object
      expiresAt: authData.expiresAt,
      refreshTokenExpiresAt: authData.refreshTokenExpiresAt
    });
    
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(authData));
    console.log('Auth data stored successfully');
  } catch (error) {
    console.error('Error storing auth data:', error);
    // Clear any partial data that might have been stored
    localStorage.removeItem(AUTH_STORAGE_KEY);
    throw error;
  }
};

const clearAuthData = (): void => {
  localStorage.removeItem(AUTH_STORAGE_KEY);
};

/**
 * Login with username and password
 */
export const login = async (username: string, password: string): Promise<AuthResponse> => {
  try {
    console.log('Attempting login with username:', username);
    console.log('API URL:', api.axiosInstance.defaults.baseURL);
    
    // Call the API login endpoint directly using axios
    const response = await api.axiosInstance.post<{
      success: boolean;
      message: string | null;
      token: string;
      user: ApiUser;
      userId: number;
    }>('/auth/login', { username, password });
    
    console.log('Login response:', response);
    
    if (!response.data) {
      throw new Error('No data in response');
    }
    
    if (!response.data.token) {
      console.error('No token in response data:', response.data);
      throw new Error('No authentication token received from server');
    }
    
    console.log('Login successful, preparing auth data');
    
    // Prepare the auth response with default values
    const authResponse: AuthResponse = {
      token: response.data.token,
      refreshToken: response.data.token, // Using the same token as refresh token
      user: response.data.user,
      expiresIn: 3600, // 1 hour in seconds
      refreshTokenExpiresIn: 86400 * 7, // 7 days in seconds
    };
    
    console.log('Storing auth data:', authResponse);
    
    // Store the auth data
    storeAuthData(authResponse);
    
    // Set the default authorization header
    api.axiosInstance.defaults.headers.common['Authorization'] = `Bearer ${authResponse.token}`;
    
    console.log('Login process completed successfully');
    return authResponse;
  } catch (error) {
    console.error('Login error:', error);
    
    if (axios.isAxiosError(error)) {
      const message = error.response?.data?.message || 'Login failed. Please check your credentials.';
      throw new AuthError(message, error.response?.data?.code || 'AUTH_FAILED');
    }
    throw new AuthError('Connection error. Please try again later.', 'CONNECTION_ERROR');
  }
};

/**
 * Refresh the access token using refresh token
 */
export const refreshToken = async (): Promise<AuthResponse> => {
  const authData = getStoredAuth();
  
  if (!authData?.refreshToken) {
    throw new AuthError('No refresh token available', 'NO_REFRESH_TOKEN');
  }
  
  try {
    // Using any here since refreshToken method is not defined in ApiService
    const response = await (api as any).refreshToken({
      refreshToken: authData.refreshToken,
    });
    
    // Convert API response to our internal AuthResponse format
    const apiUser: ApiUser = {
      id: response.user.id,
      username: response.user.username,
      email: response.user.email,
      firstName: response.user.firstName,
      lastName: response.user.lastName,
      storeId: response.user.storeId,
      roles: response.user.roles || [],
      permissions: response.user.permissions || [],
      isActive: true,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    const authResponse: AuthResponse = {
      token: response.token,
      refreshToken: response.refreshToken || '',
      user: apiUser,
      expiresIn: response.expiresIn || 3600,
      refreshTokenExpiresIn: response.refreshTokenExpiresIn || 2592000
    };
    
    storeAuthData(authResponse);
    return authResponse;
  } catch (error: any) {
    clearAuthData();
    
    if (error.response) {
      throw new AuthError(
        error.response.data?.message || 'Token refresh failed',
        error.response.data?.code || 'TOKEN_REFRESH_FAILED'
      );
    }
    throw new AuthError('Network error during token refresh', 'NETWORK_ERROR');
  }
};

/**
 * Get the current authenticated user
 */
export const getCurrentUser = (): User | null => {
  try {
    console.log('Retrieving current user from auth data...');
    const authData = getStoredAuth();
    
    if (!authData) {
      console.log('No auth data found in localStorage');
      return null;
    }
    
    // Check if token is expired
    const now = new Date();
    const tokenExpiry = new Date(authData.expiresAt);
    
    if (now > tokenExpiry) {
      console.log('Token has expired');
      clearAuthData();
      return null;
    }
    
    console.log('Current user retrieved successfully');
    return authData.user;
  } catch (error) {
    console.error('Error getting current user:', error);
    // Clear any invalid data
    clearAuthData();
    return null;
  }
};

/**
 * Get the current user with a fresh API call
 */
export const getCurrentUserWithRefresh = async (): Promise<User | null> => {
  try {
    console.log('Fetching current user from API...');
    const user = await api.getCurrentUser();
    
    const authData = getStoredAuth();
    if (authData) {
      const apiUser: ApiUser = {
        id: user.id,
        username: user.username,
        email: user.email,
        firstName: user.firstName,
        lastName: user.lastName,
        storeId: user.storeId,
        roles: user.roles || [],
        permissions: user.permissions || [],
        isActive: user.isActive !== undefined ? user.isActive : true,
        createdAt: (user as any).createdAt || new Date().toISOString(),
        updatedAt: (user as any).updatedAt || new Date().toISOString(),
      };

      const updatedAuthData: AuthResponse = {
        token: authData.token,
        refreshToken: authData.refreshToken,
        user: apiUser,
        expiresIn: Math.floor((new Date(authData.expiresAt).getTime() - new Date().getTime()) / 1000),
        refreshTokenExpiresIn: Math.floor((new Date(authData.refreshTokenExpiresAt).getTime() - new Date().getTime()) / 1000)
      };
      
      storeAuthData(updatedAuthData);
      console.log('User data refreshed from API');
    }
    
    // Convert to full User object before returning
    const fullUser: User = {
      ...user,
      lastLogin: (user as any).lastLogin || null,
      createdAt: (user as any).createdAt || new Date().toISOString(),
      updatedAt: (user as any).updatedAt || new Date().toISOString(),
    };
    
    return fullUser;
  } catch (error) {
    console.error('Failed to fetch current user from API:', error);
    // Return the cached user if available, otherwise null
    const authData = getStoredAuth();
    return authData?.user || null;
  }
};

/**
 * Logout the current user
 */
export const logout = async (): Promise<void> => {
  try {
    console.log('Logging out user...');
    const authData = getStoredAuth();
    
    if (authData?.token) {
      try {
        // Try to call the logout endpoint if it exists
        if (typeof (api as any).logout === 'function') {
          await (api as any).logout({
            refreshToken: authData.refreshToken,
          });
        }
      } catch (error) {
        console.error('Error during logout API call (proceeding with local logout):', error);
        // Continue with local logout even if API call fails
      }
    }
  } catch (error) {
    console.error('Error during logout process:', error);
  } finally {
    // Always clear auth data and redirect
    console.log('Clearing auth data and redirecting to login');
    clearAuthData();
    
    // Use window.location for a full page reload to ensure all state is cleared
    window.location.href = '/login';
  }
};

/**
 * Check if the current token is valid and not expired
 */
export const isAuthenticated = (): boolean => {
  const authData = getStoredAuth();
  
  if (!authData?.token || !authData.expiresAt) {
    return false;
  }
  
  const expiresAt = new Date(authData.expiresAt);
  return expiresAt > new Date();
};

/**
 * Check if token needs refresh (within threshold)
 */
export const shouldRefreshToken = (): boolean => {
  const authData = getStoredAuth();
  
  if (!authData?.expiresAt) {
    return false;
  }
  
  const expiresAt = new Date(authData.expiresAt);
  const now = new Date();
  const timeUntilExpiry = (expiresAt.getTime() - now.getTime()) / 1000; // in seconds
  
  return timeUntilExpiry < TOKEN_REFRESH_THRESHOLD;
};

/**
 * Get the current access token
 */
export const getAccessToken = (): string | null => {
  return getStoredAuth()?.token || null;
};

/**
 * Initialize auth state and set up token refresh if needed
 */
export const initializeAuth = async (): Promise<void> => {
  if (!isAuthenticated()) {
    if (getStoredAuth()?.refreshToken) {
      try {
        await refreshToken();
      } catch (error) {
        console.error('Failed to refresh token:', error);
        await logout();
      }
    }
  }
};
