import { useEffect, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Stack,
  TextField,
  Typography,
  Alert,
  IconButton,
  Avatar,
  Divider,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from '@mui/material';
import {
  Visibility,
  VisibilityOff,
  PhotoCamera,
} from '@mui/icons-material';
import { getUserProfile, updateProfile, changePassword, type UpdateProfileData, type ChangePasswordData } from '../api';
import { useAuth } from '../contexts/AuthContext';

export default function Profile() {
  const { user, refreshUser } = useAuth();
  const [profile, setProfile] = useState(user);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [profilePicture, setProfilePicture] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [profileLoading, setProfileLoading] = useState(true);
  const [passwordDialogOpen, setPasswordDialogOpen] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  useEffect(() => {
    const loadProfile = async () => {
      try {
        // Use cached profile from auth context first
        if (user) {
          setProfile(user);
          setFirstName(user.firstName || '');
          setLastName(user.lastName || '');
          setEmail(user.email || '');
          setProfilePicture(user.profilePicture);
        }

        // Then try to fetch fresh data from API
        const userProfile = await getUserProfile();
        setProfile(userProfile);
        setFirstName(userProfile.firstName || '');
        setLastName(userProfile.lastName || '');
        setEmail(userProfile.email || '');
        setProfilePicture(userProfile.profilePicture);
        await refreshUser(); // Update auth context with fresh data
      } catch (e: any) {
        console.error('Profile load error:', e);
        if (user) {
          setError('Could not sync with server. Showing cached profile data.');
        } else {
          setError('Failed to load profile. Please try again.');
        }
      } finally {
        setProfileLoading(false);
      }
    };

    loadProfile();
  }, [user, refreshUser]);

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    setLoading(true);

    try {
      const updateData: UpdateProfileData = {
        firstName: firstName.trim() || undefined,
        lastName: lastName.trim() || undefined,
        email: email.trim() || undefined,
        profilePicture: profilePicture,
      };
      
      const updatedProfile = await updateProfile(updateData);
      setProfile(updatedProfile);
      setSuccess('Profile updated successfully!');
    } catch (e: any) {
      setError(e.message || 'Failed to update profile');
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (newPassword !== confirmPassword) {
      setError('New passwords do not match');
      return;
    }

    if (newPassword.length < 6) {
      setError('New password must be at least 6 characters long');
      return;
    }

    setLoading(true);
    try {
      const passwordData: ChangePasswordData = {
        currentPassword,
        newPassword,
      };
      
      await changePassword(passwordData);
      setPasswordDialogOpen(false);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setSuccess('Password changed successfully!');
    } catch (e: any) {
      console.error('Password change error:', e);
      setError(e.message || 'Failed to change password');
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) { // 5MB limit
        setError('Profile picture must be less than 5MB');
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        setProfilePicture(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const getDisplayName = () => {
    if (!profile) return '';
    const fullName = [profile.firstName, profile.lastName].filter(Boolean).join(' ');
    return fullName || profile.userName;
  };

  const getAvatarText = () => {
    if (!profile) return '';
    if (profile.firstName) {
      return profile.firstName.charAt(0).toUpperCase();
    }
    return profile.userName.charAt(0).toUpperCase();
  };

  if (profileLoading) {
    return (
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: 'background.default',
        }}
      >
        <Typography>Loading profile...</Typography>
      </Box>
    );
  }

  if (!profile) {
    return (
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: 'background.default',
        }}
      >
        <Typography>Profile not found</Typography>
      </Box>
    );
  }

  return (
    <>
      <Container maxWidth="md" sx={{ minHeight: '100vh', py: 4 }}>
        <Stack spacing={3}>
          {/* Header */}
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600, mb: 1 }}>
            User Profile
          </Typography>

          {/* Profile Picture Card */}
          <Card>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={3}>
                <Box position="relative">
                  <Avatar
                    src={profilePicture}
                    sx={{ width: 100, height: 100, fontSize: '2rem' }}
                  >
                    {!profilePicture && getAvatarText()}
                  </Avatar>
                  <IconButton
                    component="label"
                    sx={{
                      position: 'absolute',
                      bottom: -8,
                      right: -8,
                      bgcolor: 'primary.main',
                      color: 'white',
                      '&:hover': { bgcolor: 'primary.dark' },
                    }}
                    size="small"
                  >
                    <PhotoCamera fontSize="small" />
                    <input
                      type="file"
                      hidden
                      accept="image/*"
                      onChange={handleFileUpload}
                    />
                  </IconButton>
                </Box>
                <Box>
                  <Typography variant="h5" sx={{ fontWeight: 600 }}>
                    {getDisplayName()}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    @{profile.userName}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Member since {new Date(profile.createdAt).toLocaleDateString()}
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>

          {/* Profile Form */}
          <Card>
            <CardContent>
              <Stack spacing={3}>
                <Typography variant="h6" sx={{ fontWeight: 600 }}>
                  Profile Information
                </Typography>

                {/* Alerts */}
                {error && (
                  <Alert severity="error" sx={{ borderRadius: 1 }}>
                    {error}
                  </Alert>
                )}
                {success && (
                  <Alert severity="success" sx={{ borderRadius: 1 }}>
                    {success}
                  </Alert>
                )}

                <Box component="form" onSubmit={handleUpdateProfile}>
                  <Stack spacing={3}>
                    <TextField
                      label="Username"
                      value={profile.userName}
                      disabled
                      fullWidth
                      helperText="Username cannot be changed"
                      variant="outlined"
                      sx={{
                        '& .MuiOutlinedInput-root': {
                          borderRadius: 2,
                        },
                      }}
                    />

                    <Stack direction="row" spacing={2}>
                      <TextField
                        label="First Name"
                        value={firstName}
                        onChange={(e) => setFirstName(e.target.value)}
                        fullWidth
                        variant="outlined"
                        sx={{
                          '& .MuiOutlinedInput-root': {
                            borderRadius: 2,
                          },
                        }}
                      />
                      
                      <TextField
                        label="Last Name"
                        value={lastName}
                        onChange={(e) => setLastName(e.target.value)}
                        fullWidth
                        variant="outlined"
                        sx={{
                          '& .MuiOutlinedInput-root': {
                            borderRadius: 2,
                          },
                        }}
                      />
                    </Stack>

                    <TextField
                      label="Email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      fullWidth
                      variant="outlined"
                      sx={{
                        '& .MuiOutlinedInput-root': {
                          borderRadius: 2,
                        },
                      }}
                    />

                    <Button
                      type="submit"
                      variant="contained"
                      size="large"
                      disabled={loading}
                      sx={{
                        alignSelf: 'flex-start',
                        px: 4,
                        py: 1.5,
                        borderRadius: 2,
                        fontSize: '1rem',
                        fontWeight: 600,
                      }}
                    >
                      {loading ? 'Updating...' : 'Update Profile'}
                    </Button>
                  </Stack>
                </Box>
              </Stack>
            </CardContent>
          </Card>

          {/* Security Section */}
          <Card>
            <CardContent>
              <Stack spacing={3}>
                <Typography variant="h6" sx={{ fontWeight: 600 }}>
                  Security
                </Typography>

                <Box>
                  <Typography variant="body1" sx={{ mb: 1 }}>
                    Password
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Keep your account secure with a strong password
                  </Typography>
                  <Button
                    variant="outlined"
                    onClick={() => {
                      setError(null);
                      setSuccess(null);
                      setPasswordDialogOpen(true);
                    }}
                    sx={{
                      px: 3,
                      py: 1,
                      borderRadius: 2,
                      fontWeight: 600,
                    }}
                  >
                    Change Password
                  </Button>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        </Stack>
      </Container>

      {/* Change Password Dialog */}
      <Dialog open={passwordDialogOpen} onClose={() => setPasswordDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Change Password</DialogTitle>
        <Box component="form" onSubmit={handleChangePassword}>
          <DialogContent>
            <Stack spacing={3}>
              {/* Error Alert in Dialog */}
              {error && passwordDialogOpen && (
                <Alert severity="error" sx={{ borderRadius: 1 }}>
                  {error}
                </Alert>
              )}
              
              <TextField
                label="Current Password"
                type={showCurrentPassword ? 'text' : 'password'}
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                required
                fullWidth
                InputProps={{
                  endAdornment: (
                    <IconButton
                      onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                      edge="end"
                    >
                      {showCurrentPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  ),
                }}
              />
              
              <TextField
                label="New Password"
                type={showNewPassword ? 'text' : 'password'}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                fullWidth
                InputProps={{
                  endAdornment: (
                    <IconButton
                      onClick={() => setShowNewPassword(!showNewPassword)}
                      edge="end"
                    >
                      {showNewPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  ),
                }}
              />

              <TextField
                label="Confirm New Password"
                type={showConfirmPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                fullWidth
                InputProps={{
                  endAdornment: (
                    <IconButton
                      onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                      edge="end"
                    >
                      {showConfirmPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  ),
                }}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button 
              onClick={() => {
                setPasswordDialogOpen(false);
                setCurrentPassword('');
                setNewPassword('');
                setConfirmPassword('');
                setError(null);
              }} 
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={loading}>
              {loading ? 'Changing...' : 'Change Password'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
    </>
  );
}
