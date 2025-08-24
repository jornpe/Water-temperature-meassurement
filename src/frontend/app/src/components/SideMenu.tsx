import * as React from 'react';
import { styled } from '@mui/material/styles';
import Avatar from '@mui/material/Avatar';
import MuiDrawer, { drawerClasses } from '@mui/material/Drawer';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import MenuContent from './MenuContent';
import OptionsMenu from './OptionsMenu';
import { getCurrentUser, type UserProfile } from '../api';
import { useNavigate } from 'react-router-dom';

const drawerWidth = 240;

const Drawer = styled(MuiDrawer)({
  width: drawerWidth,
  flexShrink: 0,
  boxSizing: 'border-box',
  mt: 10,
  [`& .${drawerClasses.paper}`]: {
    width: drawerWidth,
    boxSizing: 'border-box',
  },
});

export default function SideMenu() {
  const [user, setUser] = React.useState<UserProfile | null>(getCurrentUser());
  const navigate = useNavigate();

  React.useEffect(() => {
    const handleStorageChange = () => {
      setUser(getCurrentUser());
    };

    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []);

  const handleProfileClick = () => {
    navigate('/profile');
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
      variant="permanent"
      sx={{
        display: { xs: 'none', md: 'block' },
        [`& .${drawerClasses.paper}`]: {
          backgroundColor: 'background.paper',
        },
      }}
    >
      <Box
        sx={{
          display: 'flex',
          mt: 'calc(var(--template-frame-height, 0px) + 4px)',
          p: 1.5,
        }}
      >
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Water Temp Monitor
        </Typography>
      </Box>
      <Divider />
      <Box
        sx={{
          overflow: 'auto',
          height: '100%',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
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
          src={user?.profilePicture}
          sx={{ width: 36, height: 36 }}
        >
          {!user?.profilePicture && getAvatarText()}
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
    </Drawer>
  );
}
