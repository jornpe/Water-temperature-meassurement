import * as React from 'react';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import { Thermostat, GitHub } from '@mui/icons-material';
import { useNavigate, useLocation } from 'react-router-dom';

const mainListItems = [
  { text: 'Temperature Sensors', icon: <Thermostat />, path: '/' },
];

const secondaryListItems = [
  { text: 'GitHub', icon: <GitHub />, url: 'https://github.com/jornpe/Water-temperature-meassurement' },
];

export default function MenuContent() {
  const navigate = useNavigate();
  const location = useLocation();

  const handleMainItemClick = (item: { text: string; icon: React.ReactNode; path: string }) => {
    navigate(item.path);
  };

  const handleSecondaryItemClick = (item: { text: string; icon: React.ReactNode; url?: string }) => {
    if (item.url) {
      window.open(item.url, '_blank');
    }
  };

  return (
    <Stack sx={{ flexGrow: 1, p: 1, justifyContent: 'space-between' }}>
      <List dense>
        {mainListItems.map((item, index) => (
          <ListItem key={index} disablePadding sx={{ display: 'block' }}>
            <ListItemButton
              selected={location.pathname === item.path}
              onClick={() => handleMainItemClick(item)}
              sx={{
                borderRadius: 1,
                '&.Mui-selected': {
                  backgroundColor: 'primary.main',
                  color: 'primary.contrastText',
                  '&:hover': {
                    backgroundColor: 'primary.dark',
                  },
                  '& .MuiListItemIcon-root': {
                    color: 'primary.contrastText',
                  },
                },
              }}
            >
              <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>

      <List dense>
        {secondaryListItems.map((item, index) => (
          <ListItem key={index} disablePadding sx={{ display: 'block' }}>
            <ListItemButton 
              onClick={() => handleSecondaryItemClick(item)}
              sx={{ borderRadius: 1 }}
            >
              <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Stack>
  );
}
