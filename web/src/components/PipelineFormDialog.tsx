import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { getJobs } from '../api/jobsApi';
import WorkflowCanvas from './workflow/WorkflowCanvas';
import type { CreatePipelinePayload, PipelineDefinition } from '../types';

interface StepDraft {
  id: string;
  stepName: string;
  jobDefinitionId: string;
}

interface Props {
  open: boolean;
  pipeline?: PipelineDefinition;
  onClose: () => void;
  onSubmit: (payload: CreatePipelinePayload) => void;
}

export default function PipelineFormDialog({ open, pipeline, onClose, onSubmit }: Props) {
  const [name, setName] = useState(pipeline?.name ?? '');
  const [description, setDescription] = useState(pipeline?.description ?? '');
  const [enabled, setEnabled] = useState(pipeline?.enabled ?? true);
  const [steps, setSteps] = useState<StepDraft[]>(
    pipeline?.steps.map(s => ({
      id: crypto.randomUUID(),
      stepName: s.stepName,
      jobDefinitionId: s.jobDefinitionId,
    })) ?? []
  );

  const { data: batchJobs = [] } = useQuery({ queryKey: ['jobs', 'Batch'], queryFn: () => getJobs('Batch') });
  const { data: fnJobs = [] } = useQuery({ queryKey: ['jobs', 'Function'], queryFn: () => getJobs('Function') });
  const allJobs = [...batchJobs, ...fnJobs];

  const handleSubmit = () => {
    if (!name.trim() || steps.length === 0) return;
    onSubmit({
      name: name.trim(),
      description: description.trim(),
      enabled,
      steps: steps.map(s => ({ stepName: s.stepName, jobDefinitionId: s.jobDefinitionId })),
    });
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        {pipeline ? 'Edit Pipeline' : 'New Pipeline'}
      </DialogTitle>
      <Divider />
      <DialogContent>
        <Stack spacing={2.5} mt={1}>
          <TextField
            label="Pipeline Name"
            required
            value={name}
            onChange={e => setName(e.target.value)}
            fullWidth
          />
          <TextField
            label="Description"
            value={description}
            onChange={e => setDescription(e.target.value)}
            multiline
            rows={2}
            fullWidth
          />
          <FormControlLabel
            control={<Switch checked={enabled} onChange={e => setEnabled(e.target.checked)} />}
            label="Enabled"
          />

          <Box>
            <Typography variant="subtitle2" fontWeight={600} gutterBottom>
              Pipeline Steps ({steps.length})
            </Typography>
            <Typography variant="caption" color="text.secondary" display="block" mb={2}>
              Drag steps to reorder. Each step runs sequentially — the previous step's output
              is available as <code>previousStepOutput</code> in the next step's parameters.
            </Typography>

            <WorkflowCanvas
              steps={steps}
              availableJobs={allJobs}
              onChange={setSteps}
            />
          </Box>
        </Stack>
      </DialogContent>
      <Divider />
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose}>Cancel</Button>
        <Box sx={{ flex: 1 }} />
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!name.trim() || steps.length === 0}
        >
          {pipeline ? 'Save Changes' : 'Create Pipeline'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
