import { createTheme } from '@mui/material/styles';

const azureBlue = '#0078d4';
const azureDark = '#005a9e';
const purple = '#8764B8';
const teal = '#00B4D8';

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: azureBlue, dark: azureDark, light: '#1890ff' },
    secondary: { main: purple },
    success: { main: '#107C10' },
    error: { main: '#A4262C' },
    warning: { main: '#D83B01' },
    info: { main: teal },
    background: { default: '#f0f4f8', paper: '#ffffff' },
    text: { primary: '#201f1e', secondary: '#605e5c' },
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: '"Segoe UI", -apple-system, BlinkMacSystemFont, Roboto, sans-serif',
    h4: { fontWeight: 700 },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 600 },
    subtitle1: { fontWeight: 600 },
    subtitle2: { fontWeight: 600 },
  },
  components: {
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: { border: '1px solid #e1dfdd', borderRadius: 12 },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: { textTransform: 'none', fontWeight: 600, borderRadius: 6 },
        contained: { boxShadow: 'none', '&:hover': { boxShadow: '0 2px 8px rgba(0,120,212,0.3)' } },
      },
    },
    MuiChip: {
      styleOverrides: { root: { fontWeight: 600 } },
    },
    MuiTableCell: {
      styleOverrides: { head: { fontWeight: 700, backgroundColor: '#faf9f8' } },
    },
    MuiDrawer: {
      styleOverrides: { paper: { border: 'none', boxShadow: '0 8px 32px rgba(0,0,0,0.12)' } },
    },
  },
});
