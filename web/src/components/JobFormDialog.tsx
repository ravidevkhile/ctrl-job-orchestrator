import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
} from '@mui/material';
import { useState } from 'react';
import type { CreateJobPayload, JobDefinition, JobTarget } from '../types';
import { JOB_TYPES } from '../types';

interface Props {
  open: boolean;
  target: JobTarget;
  job?: JobDefinition;
  onClose: () => void;
  onSubmit: (payload: CreateJobPayload) => void;
}

export default function JobFormDialog({ open, target, job, onClose, onSubmit }: Props) {
  const [name, setName] = useState(job?.name ?? '');
  const [jobType, setJobType] = useState(job?.jobType ?? JOB_TYPES[target][0]);
  const [parametersJson, setParametersJson] = useState(job?.parametersJson ?? '{}');
  const [cronSchedule, setCronSchedule] = useState(job?.cronSchedule ?? '');
  const [enabled, setEnabled] = useState(job?.enabled ?? true);
  const [jsonError, setJsonError] = useState('');

  const validateJson = (value: string) => {
    try {
      JSON.parse(value);
      setJsonError('');
      return true;
    } catch {
      setJsonError('Invalid JSON');
      return false;
    }
  };

  const handleSubmit = () => {
    if (!name.trim()) return;
    if (!validateJson(parametersJson)) return;
    onSubmit({
      name: name.trim(),
      target,
      jobType,
      parametersJson,
      cronSchedule: cronSchedule.trim() || undefined,
      enabled,
    });
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{job ? 'Edit Job' : 'New Job'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          <TextField
            label="Name"
            required
            value={name}
            onChange={e => setName(e.target.value)}
            fullWidth
          />
          <TextField
            label="Job Type"
            select
            value={jobType}
            onChange={e => setJobType(e.target.value)}
            fullWidth
          >
            {JOB_TYPES[target].map(t => (
              <MenuItem key={t} value={t}>{t}</MenuItem>
            ))}
          </TextField>
          <TextField
            label="Parameters (JSON)"
            multiline
            rows={4}
            value={parametersJson}
            onChange={e => {
              setParametersJson(e.target.value);
              validateJson(e.target.value);
            }}
            error={Boolean(jsonError)}
            helperText={jsonError || 'Free-form JSON forwarded to the executor'}
            fullWidth
            InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
          />
          <TextField
            label="Cron Schedule (optional)"
            value={cronSchedule}
            onChange={e => setCronSchedule(e.target.value)}
            placeholder="0 6 * * *"
            helperText="Standard cron expression — leave blank for manual-only"
            fullWidth
          />
          <FormControlLabel
            control={<Switch checked={enabled} onChange={e => setEnabled(e.target.checked)} />}
            label="Enabled"
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={!name.trim()}>
          {job ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
