import { useEffect, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  FormControl,
  Link,
  Stack,
  TextField,
  Typography,
  Alert,
  IconButton,
} from '@mui/material';
import {
  Visibility,
  VisibilityOff,
} from '@mui/icons-material';
import { login, usersExist } from '../api';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Login() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const { login: authLogin } = useAuth();

  useEffect(() => {
    usersExist().then((exists) => {
      if (!exists) navigate('/register', { replace: true });
    });
  }, [navigate]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const user = await login(userName, password);
      authLogin(user); // Update auth context
      navigate('/', { replace: true });
    } catch (e: any) {
      setError('Invalid credentials');
    } finally {
      setLoading(false);
    }
  };

  const handleClickShowPassword = () => setShowPassword(!showPassword);

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
        py: 3,
      }}
    >
      <Container maxWidth="sm">
        <Card
          sx={{
            p: { xs: 2, sm: 4 },
            boxShadow: {
              xs: 'none',
              sm: '0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)',
            },
            border: {
              xs: 'none',
              sm: '1px solid',
            },
            borderColor: 'divider',
          }}
        >
          <CardContent>
            <Stack spacing={3}>
              {/* Header */}
              <Stack spacing={1} alignItems="center">
                <Typography
                  variant="h4"
                  component="h1"
                  sx={{ fontWeight: 600, color: 'text.primary' }}
                >
                  Sign In
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Welcome back! Please sign in to your account
                </Typography>
              </Stack>

              {/* Error Alert */}
              {error && (
                <Alert severity="error" sx={{ borderRadius: 1 }}>
                  {error}
                </Alert>
              )}

              {/* Form */}
              <Box component="form" onSubmit={submit}>
                <Stack spacing={2}>
                  <TextField
                    label="Username"
                    value={userName}
                    onChange={(e) => setUserName(e.target.value)}
                    required
                    fullWidth
                    autoFocus
                    variant="outlined"
                    sx={{
                      '& .MuiOutlinedInput-root': {
                        borderRadius: 2,
                      },
                    }}
                  />
                  
                  <TextField
                    label="Password"
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    fullWidth
                    variant="outlined"
                    InputProps={{
                      endAdornment: (
                        <IconButton
                          aria-label="toggle password visibility"
                          onClick={handleClickShowPassword}
                          edge="end"
                        >
                          {showPassword ? <VisibilityOff /> : <Visibility />}
                        </IconButton>
                      ),
                    }}
                    sx={{
                      '& .MuiOutlinedInput-root': {
                        borderRadius: 2,
                      },
                    }}
                  />

                  {/* Sign In Button */}
                  <Button
                    type="submit"
                    variant="contained"
                    fullWidth
                    size="large"
                    disabled={loading}
                    sx={{
                      mt: 2,
                      py: 1.5,
                      borderRadius: 2,
                      fontSize: '1rem',
                      fontWeight: 600,
                    }}
                  >
                    {loading ? 'Signing in...' : 'Sign In'}
                  </Button>
                </Stack>
              </Box>

              {/* Sign up link */}
              <Box textAlign="center">
                <Typography variant="body2" color="text.secondary">
                  Don't have an account?{' '}
                  <Link
                    href="/register"
                    color="primary"
                    sx={{ 
                      textDecoration: 'none', 
                      fontWeight: 600,
                      '&:hover': { textDecoration: 'underline' }
                    }}
                  >
                    Sign up
                  </Link>
                </Typography>
              </Box>
            </Stack>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}
