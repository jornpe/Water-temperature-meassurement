import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { getCurrentUser, authHeader, logout as apiLogout, usersExist, type UserProfile } from '../api';

interface AuthContextType {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (user: UserProfile) => void;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const navigate = useNavigate();
  const location = useLocation();

  const isAuthenticated = !!user && !!authHeader().Authorization;

  // Check authentication status on mount and when local storage changes
  useEffect(() => {
    const checkAuth = () => {
      const currentUser = getCurrentUser();
      const hasToken = !!authHeader().Authorization;
      
      if (currentUser && hasToken) {
        setUser(currentUser);
      } else {
        setUser(null);
      }
      
      setIsLoading(false);
    };

    checkAuth();

    // Listen for storage changes (in case user logs out in another tab)
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === 'user' || e.key === 'token') {
        checkAuth();
      }
    };

    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []);

  // Redirect logic for authentication
  useEffect(() => {
    if (!isLoading) {
      const checkInitialSetup = async () => {
        try {
          const hasUsers = await usersExist();
          
          // Don't redirect if we're already on login/register pages
          const publicPaths = ['/login', '/register'];
          const isPublicPath = publicPaths.includes(location.pathname);

          if (!hasUsers && !isAuthenticated && !isPublicPath) {
            // No users exist and not authenticated, redirect to register
            navigate('/register', { replace: true });
            return;
          }

          if (!isAuthenticated && !isPublicPath) {
            // Not authenticated and not on a public page, redirect to login
            navigate('/login', { replace: true });
            return;
          }
        } catch (error) {
          console.error('Failed to check initial setup:', error);
          // On error, redirect to login if not authenticated
          if (!isAuthenticated && !location.pathname.startsWith('/login')) {
            navigate('/login', { replace: true });
          }
        }
      };

      checkInitialSetup();
    }
  }, [isAuthenticated, isLoading, navigate, location.pathname]);

  const login = useCallback((userProfile: UserProfile) => {
    setUser(userProfile);
  }, []);

  const logout = useCallback(() => {
    apiLogout(); // Clear localStorage
    setUser(null);
    navigate('/login', { replace: true });
  }, [navigate]);

  const refreshUser = useCallback(async () => {
    try {
      // This would typically fetch fresh user data from the API
      const currentUser = getCurrentUser();
      if (currentUser) {
        setUser(currentUser);
      }
    } catch (error) {
      console.error('Failed to refresh user:', error);
      // On refresh error, might want to logout
      logout();
    }
  }, [logout]);

  const value: AuthContextType = {
    user,
    isAuthenticated,
    isLoading,
    login,
    logout,
    refreshUser,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
