import AccountTreeIcon from '@mui/icons-material/AccountTree';
import BarChartIcon from '@mui/icons-material/BarChart';
import CloudIcon from '@mui/icons-material/Cloud';
import EmailIcon from '@mui/icons-material/Email';
import ExtensionIcon from '@mui/icons-material/Extension';
import FunctionsIcon from '@mui/icons-material/Functions';
import {
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';

export type SectionId = 'dashboard' | 'batch' | 'functions' | 'pipelines' | 'servicebus' | 'templates';

interface NavItem {
  id: SectionId;
  label: string;
  icon: React.ReactNode;
}

const navItems: NavItem[] = [
  { id: 'dashboard', label: 'Dashboard', icon: <BarChartIcon /> },
  { id: 'batch', label: 'Azure Batch', icon: <CloudIcon /> },
  { id: 'functions', label: 'Azure Functions', icon: <FunctionsIcon /> },
  { id: 'pipelines', label: 'Pipelines', icon: <AccountTreeIcon /> },
  { id: 'servicebus', label: 'Service Bus', icon: <EmailIcon /> },
  { id: 'templates', label: 'Templates', icon: <ExtensionIcon /> },
];

const DRAWER_WIDTH = 240;

interface Props {
  activeSection: SectionId;
  onNavigate: (section: SectionId) => void;
  mobileOpen?: boolean;
  onMobileClose?: () => void;
}

function SidebarContent({ activeSection, onNavigate }: Props) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Toolbar sx={{ px: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        <CloudIcon sx={{ mr: 1.5, color: 'primary.main', fontSize: 28 }} />
        <Box>
          <Typography variant="subtitle2" fontWeight={700} lineHeight={1.2}>
            Azure Job
          </Typography>
          <Typography variant="caption" color="text.secondary" lineHeight={1}>
            Orchestrator
          </Typography>
        </Box>
      </Toolbar>
      <List sx={{ flex: 1, pt: 1.5, px: 1 }}>
        {navItems.map(item => {
          const active = activeSection === item.id;
          return (
            <ListItemButton
              key={item.id}
              selected={active}
              onClick={() => onNavigate(item.id)}
              sx={{
                borderRadius: 2,
                mb: 0.5,
                '&.Mui-selected': {
                  bgcolor: 'primary.main',
                  color: 'white',
                  '& .MuiListItemIcon-root': { color: 'white' },
                  '&:hover': { bgcolor: 'primary.dark' },
                },
                '&:hover': {
                  bgcolor: active ? 'primary.dark' : 'action.hover',
                },
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 36,
                  color: active ? 'white' : 'text.secondary',
                }}
              >
                {item.icon}
              </ListItemIcon>
              <ListItemText
                primary={item.label}
                primaryTypographyProps={{
                  variant: 'body2',
                  fontWeight: active ? 700 : 500,
                }}
              />
            </ListItemButton>
          );
        })}
      </List>
      <Box sx={{ p: 2, borderTop: '1px solid', borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          .NET 10 · React 18 · MUI v6
        </Typography>
      </Box>
    </Box>
  );
}

export default function Sidebar(props: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  if (isMobile) {
    return (
      <Drawer
        variant="temporary"
        open={props.mobileOpen}
        onClose={props.onMobileClose}
        ModalProps={{ keepMounted: true }}
        sx={{
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
          },
        }}
      >
        <SidebarContent {...props} />
      </Drawer>
    );
  }

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: DRAWER_WIDTH,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: DRAWER_WIDTH,
          boxSizing: 'border-box',
        },
      }}
    >
      <SidebarContent {...props} />
    </Drawer>
  );
}

export { DRAWER_WIDTH };
