import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';

const API_URL = process.env.REACT_APP_API_URL || 'https://localhost:7287/api';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  user: {
    id: number;
    username: string;
    email: string;
    firstName: string;
    lastName: string;
    storeId: number | null;
    roles: string[];
    permissions: string[];
  };
}

class ApiService {
  public axiosInstance: AxiosInstance;

  constructor() {
    this.axiosInstance = axios.create({
      baseURL: API_URL,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Add request interceptor to add auth token
    this.axiosInstance.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem('token');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => {
        return Promise.reject(error);
      }
    );
  }

  // Auth methods
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    try {
      const response = await this.axiosInstance.post<AuthResponse>('/auth/login', {
        username: credentials.username,
        password: credentials.password
      });
      
      if (response.data && response.data.token) {
        // Store the token and user data in auth service
        localStorage.setItem('token', response.data.token);
        if (response.data.user) {
          localStorage.setItem('user', JSON.stringify(response.data.user));
        }
        return response.data;
      }
      throw new Error('Invalid response from server');
    } catch (error) {
      console.error('Login error:', error);
      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.message || 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.';
        throw new Error(message);
      }
      throw new Error('Bağlantı hatası. Lütfen daha sonra tekrar deneyin.');
    }
  }

  logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  }

  async getCurrentUser() {
    try {
      const response = await this.axiosInstance.get('/auth/me');
      if (response.data) {
        // Update the stored user data
        localStorage.setItem('user', JSON.stringify(response.data));
        return response.data;
      }
      return null;
    } catch (error) {
      console.error('Failed to fetch current user', error);
      // Clear invalid token if request fails
      if (axios.isAxiosError(error) && error.response?.status === 401) {
        localStorage.removeItem('token');
      }
      return null;
    }
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('token');
  }

  // Helper method for making authenticated requests
  async request<T>(config: AxiosRequestConfig): Promise<T> {
    try {
      const response = await this.axiosInstance.request<T>(config);
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.message || error.message;
        throw new Error(message);
      }
      throw error;
    }
  }
}

export const apiService = new ApiService();
export default apiService;