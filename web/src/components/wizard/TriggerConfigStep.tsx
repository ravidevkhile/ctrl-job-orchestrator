import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import {
  Alert,
  Box,
  IconButton,
  MenuItem,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import toast from 'react-hot-toast';
import type { TriggerConfig, TriggerConfigType } from '../../types';

const TIMEZONES = [
  'UTC',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Europe/London',
  'Europe/Paris',
  'Europe/Berlin',
  'Asia/Tokyo',
  'Asia/Shanghai',
  'Asia/Kolkata',
  'Australia/Sydney',
];

const CRON_EXAMPLES = [
  { label: 'Every minute', expr: '* * * * *' },
  { label: 'Every 5 min', expr: '*/5 * * * *' },
  { label: 'Daily at 6am', expr: '0 6 * * *' },
  { label: 'Weekdays at 8am', expr: '0 8 * * MON-FRI' },
  { label: 'Weekly Monday', expr: '0 8 * * MON' },
  { label: 'Monthly 1st', expr: '0 0 1 * *' },
];

const TAB_TYPES: TriggerConfigType[] = ['Manual', 'Cron', 'Http', 'ServiceBus'];

interface Props {
  jobId?: string;
  value: TriggerConfig;
  onChange: (config: TriggerConfig) => void;
}

export default function TriggerConfigStep({ jobId, value, onChange }: Props) {
  const activeTab = TAB_TYPES.indexOf(value.type);

  const handleTabChange = (_: React.SyntheticEvent, idx: number) => {
    onChange({ ...value, type: TAB_TYPES[idx] });
  };

  const update = (patch: Partial<TriggerConfig>) => onChange({ ...value, ...patch });

  const triggerUrl = jobId
    ? `${window.location.origin}/api/triggers/${jobId}/http`
    : '/api/triggers/{jobId}/http (saved after creation)';

  const copyUrl = () => {
    navigator.clipboard.writeText(triggerUrl);
    toast.success('URL copied!');
  };

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" mb={2}>
        Choose how this job gets triggered. You can change this later.
      </Typography>
      <Tabs
        value={activeTab >= 0 ? activeTab : 0}
        onChange={handleTabChange}
        sx={{ borderBottom: 1, borderColor: 'divider', mb: 3 }}
      >
        <Tab label="Manual" />
        <Tab label="Cron Schedule" />
        <Tab label="HTTP Webhook" />
        <Tab label="Service Bus" />
      </Tabs>

      {/* Manual */}
      {value.type === 'Manual' && (
        <Alert severity="info" sx={{ borderRadius: 2 }}>
          This job will only run when manually triggered via the UI or the API.
          You can always trigger it manually regardless of the trigger type selected.
        </Alert>
      )}

      {/* Cron */}
      {value.type === 'Cron' && (
        <Stack spacing={2.5}>
          <TextField
            label="Cron Expression"
            value={value.cronExpression ?? ''}
            onChange={e => update({ cronExpression: e.target.value })}
            placeholder="0 6 * * *"
            helperText="Standard 5-field cron: min hour day month weekday"
            fullWidth
          />
          <Box>
            <Typography variant="caption" color="text.secondary" gutterBottom>
              Quick examples:
            </Typography>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap mt={0.5}>
              {CRON_EXAMPLES.map(ex => (
                <Box
                  key={ex.expr}
                  onClick={() => update({ cronExpression: ex.expr })}
                  sx={{
                    cursor: 'pointer',
                    px: 1.5,
                    py: 0.5,
                    borderRadius: 1,
                    border: '1px solid',
                    borderColor: value.cronExpression === ex.expr ? 'primary.main' : 'divider',
                    bgcolor: value.cronExpression === ex.expr ? 'primary.50' : 'background.paper',
                    '&:hover': { borderColor: 'primary.main' },
                  }}
                >
                  <Typography variant="caption" fontWeight={600}>
                    {ex.label}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                    {ex.expr}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </Box>
          <TextField
            label="Timezone"
            select
            value={value.timeZone ?? 'UTC'}
            onChange={e => update({ timeZone: e.target.value })}
            fullWidth
          >
            {TIMEZONES.map(tz => (
              <MenuItem key={tz} value={tz}>
                {tz}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      )}

      {/* HTTP */}
      {value.type === 'Http' && (
        <Stack spacing={2.5}>
          <Box
            sx={{
              p: 2,
              bgcolor: 'grey.50',
              borderRadius: 2,
              border: '1px solid',
              borderColor: 'divider',
            }}
          >
            <Typography variant="caption" color="text.secondary" gutterBottom>
              Trigger URL (POST)
            </Typography>
            <Stack direction="row" alignItems="center" spacing={1}>
              <Typography variant="body2" sx={{ fontFamily: 'monospace', flex: 1, wordBreak: 'break-all' }}>
                {triggerUrl}
              </Typography>
              <Tooltip title="Copy URL">
                <IconButton size="small" onClick={copyUrl}>
                  <ContentCopyIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </Stack>
          </Box>
          <TextField
            label="Auth Token (optional)"
            value={value.httpToken ?? ''}
            onChange={e => update({ httpToken: e.target.value })}
            helperText="If set, callers must pass this in the X-Trigger-Token header"
            type="password"
            fullWidth
          />
          <Alert severity="info">
            Call this endpoint with a POST request to trigger the job externally.
            Include the token in header: <code>X-Trigger-Token: your-token</code>
          </Alert>
        </Stack>
      )}

      {/* Service Bus */}
      {value.type === 'ServiceBus' && (
        <Stack spacing={2.5}>
          <Alert severity="warning">
            Service Bus trigger requires the ServiceBusListenerService to be running
            and a valid connection string stored in Key Vault.
          </Alert>
          <TextField
            label="Key Vault Secret Name (connection string)"
            value={value.serviceBusKeyVaultSecretName ?? ''}
            onChange={e => update({ serviceBusKeyVaultSecretName: e.target.value })}
            helperText="Name of the Key Vault secret containing the Service Bus connection string"
            fullWidth
          />
          <TextField
            label="Queue Name"
            value={value.serviceBusQueueName ?? ''}
            onChange={e => update({ serviceBusQueueName: e.target.value })}
            placeholder="job-triggers"
            fullWidth
          />
          <TextField
            label="Message Filter (optional)"
            value={value.serviceBusMessageFilter ?? ''}
            onChange={e => update({ serviceBusMessageFilter: e.target.value })}
            placeholder="jobId = 'abc123'"
            helperText="SQL-style filter applied to messages. Leave blank to accept all messages."
            fullWidth
          />
        </Stack>
      )}
    </Box>
  );
}
