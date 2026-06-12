import MenuIcon from '@mui/icons-material/Menu';
import {
  AppBar,
  Box,
  Breadcrumbs,
  IconButton,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { useState } from 'react';
import { Toaster } from 'react-hot-toast';
import Sidebar, { DRAWER_WIDTH, type SectionId } from './components/layout/Sidebar';
import DashboardTab from './tabs/DashboardTab';
import BatchTab from './tabs/BatchTab';
import FunctionsTab from './tabs/FunctionsTab';
import PipelinesTab from './tabs/PipelinesTab';
import ServiceBusTab from './tabs/ServiceBusTab';
import TemplatesTab from './tabs/TemplatesTab';

const SECTION_LABELS: Record<SectionId, string> = {
  dashboard: 'Dashboard',
  batch: 'Azure Batch',
  functions: 'Azure Functions',
  pipelines: 'Pipelines',
  servicebus: 'Service Bus',
  templates: 'Templates',
};

export default function App() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [section, setSection] = useState<SectionId>('dashboard');
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {/* Sidebar */}
      <Sidebar
        activeSection={section}
        onNavigate={s => { setSection(s); setMobileOpen(false); }}
        mobileOpen={mobileOpen}
        onMobileClose={() => setMobileOpen(false)}
      />

      {/* Main area */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          display: 'flex',
          flexDirection: 'column',
          minHeight: '100vh',
          ml: isMobile ? 0 : 0,
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
        }}
      >
        {/* Top AppBar */}
        <AppBar
          position="sticky"
          elevation={0}
          sx={{
            bgcolor: 'background.paper',
            color: 'text.primary',
            borderBottom: '1px solid',
            borderColor: 'divider',
            zIndex: theme.zIndex.drawer - 1,
          }}
        >
          <Toolbar>
            {isMobile && (
              <IconButton
                edge="start"
                onClick={() => setMobileOpen(true)}
                sx={{ mr: 2 }}
              >
                <MenuIcon />
              </IconButton>
            )}
            <Breadcrumbs>
              <Typography variant="body2" color="text.secondary">
                Azure Job Orchestrator
              </Typography>
              <Typography variant="body2" color="text.primary" fontWeight={600}>
                {SECTION_LABELS[section]}
              </Typography>
            </Breadcrumbs>
            <Box sx={{ flex: 1 }} />
            <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', sm: 'block' } }}>
              .NET 10 · React 18 · MUI v6
            </Typography>
          </Toolbar>
        </AppBar>

        {/* Content */}
        <Box sx={{ flex: 1, p: { xs: 2, md: 3 }, bgcolor: 'background.default' }}>
          {section === 'dashboard' && <DashboardTab />}
          {section === 'batch' && <BatchTab />}
          {section === 'functions' && <FunctionsTab />}
          {section === 'pipelines' && <PipelinesTab />}
          {section === 'servicebus' && <ServiceBusTab />}
          {section === 'templates' && <TemplatesTab />}
        </Box>
      </Box>

      <Toaster position="bottom-right" />
    </Box>
  );
}
