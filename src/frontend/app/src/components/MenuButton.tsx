import * as React from 'react';
import { styled } from '@mui/material/styles';
import IconButton from '@mui/material/IconButton';

const MenuButton = styled(IconButton)(({ theme }) => {
  const palette = theme.vars ?? theme;

  return {
    boxSizing: 'border-box',
    width: '2.25rem',
    height: '2.25rem',
    color: palette.palette.text.primary,
    borderRadius: theme.shape.borderRadius,
    border: `1px solid ${palette.palette.divider}`,
    backgroundColor: palette.palette.background.paper,
    boxShadow: '0 0 0 0 rgba(0, 0, 0, 0.01)',
    '&:hover': {
      backgroundColor: palette.palette.action.hover,
      borderColor: palette.palette.action.hover,
    },
  };
});

export default MenuButton;
