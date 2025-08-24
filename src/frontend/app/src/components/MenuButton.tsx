import * as React from 'react';
import { styled } from '@mui/material/styles';
import IconButton from '@mui/material/IconButton';

const MenuButton = styled(IconButton)(({ theme }) => ({
  boxSizing: 'border-box',
  width: '2.25rem',
  height: '2.25rem',
  color: theme.palette.text.primary,
  borderRadius: theme.shape.borderRadius,
  border: `1px solid ${theme.palette.divider}`,
  backgroundColor: theme.palette.background.paper,
  boxShadow: '0 0 0 0 rgba(0, 0, 0, 0.01)',
  '&:hover': {
    backgroundColor: theme.palette.action.hover,
    borderColor: theme.palette.action.hover,
  },
}));

export default MenuButton;
