// In-memory token storage
let accessToken: string | null = null;

export function setAccessToken(token: string) {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function clearAccessToken() {
  accessToken = null;
}

export function getAuthHeader(): Record<string, string> {
  return accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
}

// Refresh token function
export async function refreshAccessToken(): Promise<string | null> {
  try {
    console.log('Attempting to refresh access token...');
    const response = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include', // Include refresh token cookie
      headers: {
        'Content-Type': 'application/json',
        // Don't include Authorization header - refresh endpoint doesn't need it
      }
    });

    if (!response.ok) {
      console.log(`Refresh token failed with status: ${response.status}`);
      const errorText = await response.text().catch(() => 'Unknown error');
      console.log(`Refresh error response: ${errorText}`);
      return null; // Refresh failed
    }

    const data = await response.json();
    setAccessToken(data.token);
    console.log('Access token refreshed successfully');
    return data.token;
  } catch (error) {
    console.error('Token refresh failed:', error);
    return null;
  }
}

// Enhanced fetch function with automatic retry on 401
export async function apiRequest(
  url: string, 
  options: RequestInit = {}
): Promise<Response> {
  // Ensure credentials are included for cookie handling
  const requestOptions: RequestInit = {
    ...options,
    credentials: 'include',
  };

  // Only set Content-Type to JSON if it's not already set (e.g., for FormData)
  const isFormData = options.body instanceof FormData;
  if (!isFormData && (!options.headers || !Object.keys(options.headers).some(key => key.toLowerCase() === 'content-type'))) {
    requestOptions.headers = {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
      ...options.headers,
    };
  } else {
    requestOptions.headers = {
      ...getAuthHeader(),
      ...options.headers,
    };
  }

  let response = await fetch(url, requestOptions);

  // If we get a 401, try to refresh the token and retry once
  // Only attempt refresh if we originally had a token or if this wasn't the refresh endpoint
  if (response.status === 401 && !url.includes('/auth/refresh')) {
    console.log('Received 401, attempting token refresh...');
    const newToken = await refreshAccessToken();
    
    if (newToken) {
      console.log('Token refreshed, retrying original request...');
      // Retry the request with the new token
      const retryOptions: RequestInit = {
        ...requestOptions,
        headers: {
          ...requestOptions.headers,
          Authorization: `Bearer ${newToken}`,
        },
      };
      
      response = await fetch(url, retryOptions);
    } else {
      console.log('Token refresh failed, request will fail with 401');
    }
  }

  return response;
}

// Wrapper for API calls that automatically handles authentication
export async function authenticatedFetch(
  url: string, 
  options: RequestInit = {}
): Promise<any> {
  const response = await apiRequest(url, options);
  
  if (!response.ok) {
    if (response.status === 401) {
      // Even after refresh attempt, still 401 - redirect to login
      clearAccessToken();
      localStorage.removeItem('user');
      window.location.href = '/login';
      throw new Error('Authentication failed');
    }
    
    // Try to get error message from response
    let errorMessage = `API ${response.status}`;
    try {
      const errorData = await response.json();
      errorMessage = errorData.message || errorMessage;
    } catch {
      // If we can't parse the error response, use the default message
    }
    
    throw new Error(errorMessage);
  }

  // Return parsed JSON if there's content, otherwise return response
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    return response.json();
  }
  
  return response;
}

// Logout function that clears everything
export async function logoutUser(): Promise<void> {
  try {
    await fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'include',
      headers: getAuthHeader(),
    });
  } catch (error) {
    console.error('Logout API call failed:', error);
  } finally {
    // Clear everything regardless of API call success
    clearAccessToken();
    localStorage.removeItem('user');
  }
}

// Try to restore session on app startup
export async function tryRestoreSession(): Promise<boolean> {
  try {
    console.log('Attempting to restore session...');
    // Try to refresh the token using the HttpOnly cookie
    const newToken = await refreshAccessToken();
    if (newToken) {
      console.log('Session restored successfully');
      return true;
    } else {
      console.log('No valid refresh token found');
      return false;
    }
  } catch (error) {
    console.error('Session restore failed:', error);
    return false;
  }
}
