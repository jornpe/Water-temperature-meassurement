import * as React from 'react';
import Drawer from '@mui/material/Drawer';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Avatar from '@mui/material/Avatar';
import MenuContent from './MenuContent';
import OptionsMenu from './OptionsMenu';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { getProfilePictureUrl } from '../api';

interface SideMenuMobileProps {
  open: boolean;
  toggleDrawer: (open: boolean) => () => void;
}

export default function SideMenuMobile({ open, toggleDrawer }: SideMenuMobileProps) {
  const navigate = useNavigate();
  const { user } = useAuth();

  const handleProfileClick = () => {
    navigate('/profile');
    toggleDrawer(false)(); // Close the drawer when navigating
  };

  const getDisplayName = () => {
    if (!user) return 'User';
    const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ');
    return fullName || user.userName;
  };

  const getAvatarText = () => {
    if (!user) return 'U';
    if (user.firstName) {
      return user.firstName.charAt(0).toUpperCase();
    }
    return user.userName.charAt(0).toUpperCase();
  };

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={toggleDrawer(false)}
      sx={{
        [`& .MuiDrawer-paper`]: {
          backgroundImage: 'none',
          backgroundColor: 'background.paper',
          width: { xs: '280px', sm: '320px' }, // Responsive width
        },
      }}
      // Add modern mobile drawer behavior
      variant="temporary"
      ModalProps={{
        keepMounted: true, // Better open performance on mobile.
      }}
    >
      <Box
        sx={{
          minWidth: '60dvw',
          p: 2,
          backgroundColor: 'background.paper',
          flexGrow: 1,
          display: 'flex',
          flexDirection: 'column',
          height: '100vh',
        }}
      >
        <Box
          sx={{
            display: 'flex',
            mt: 1,
            p: 1.5,
            alignItems: 'center',
          }}
        >
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Water Temp Monitor
          </Typography>
        </Box>
        <Divider />
        <Box sx={{ flex: 1, overflow: 'auto' }}>
          <MenuContent />
        </Box>
        <Stack
          direction="row"
          sx={{
            p: 2,
            gap: 1,
            alignItems: 'center',
            borderTop: '1px solid',
            borderColor: 'divider',
            cursor: 'pointer',
            '&:hover': {
              bgcolor: 'action.hover',
            },
          }}
          onClick={handleProfileClick}
        >
          <Avatar
            sizes="small"
            src={user?.hasProfilePicture ? getProfilePictureUrl(user.id) : undefined}
            sx={{ width: 36, height: 36 }}
          >
            {!user?.hasProfilePicture && getAvatarText()}
          </Avatar>
          <Box sx={{ mr: 'auto' }}>
            <Typography variant="body2" sx={{ fontWeight: 500, lineHeight: '16px' }}>
              {getDisplayName()}
            </Typography>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              @{user?.userName || 'username'}
            </Typography>
          </Box>
          <OptionsMenu />
        </Stack>
      </Box>
    </Drawer>
  );
}
