import * as React from 'react';
import Drawer from '@mui/material/Drawer';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Avatar from '@mui/material/Avatar';
import MenuContent from './MenuContent';

interface SideMenuMobileProps {
  open: boolean;
  toggleDrawer: (open: boolean) => () => void;
}

export default function SideMenuMobile({ open, toggleDrawer }: SideMenuMobileProps) {
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={toggleDrawer(false)}
      sx={{
        [`& .MuiDrawer-paper`]: {
          backgroundImage: 'none',
          backgroundColor: 'background.paper',
        },
      }}
    >
      <Box
        sx={{
          minWidth: '60dvw',
          p: 2,
          backgroundColor: 'background.paper',
          flexGrow: 1,
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
        <MenuContent />
        <Stack
          direction="row"
          sx={{
            p: 2,
            gap: 1,
            alignItems: 'center',
            borderTop: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Avatar
            sizes="small"
            alt="Riley Carter"
            src="/static/images/avatar/7.jpg"
            sx={{ width: 36, height: 36 }}
          />
          <Box sx={{ mr: 'auto' }}>
            <Typography variant="body2" sx={{ fontWeight: 500, lineHeight: '16px' }}>
              Riley Carter
            </Typography>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              riley@email.com
            </Typography>
          </Box>
        </Stack>
      </Box>
    </Drawer>
  );
}
