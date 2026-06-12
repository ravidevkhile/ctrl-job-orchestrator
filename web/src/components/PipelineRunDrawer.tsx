import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import {
  Box,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
  Stack,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getPipelineRun, getPipelineRuns } from '../api/pipelinesApi';
import type { PipelineDefinition, PipelineRun, PipelineStepStatus } from '../types';

interface Props {
  pipeline: PipelineDefinition | null;
  onClose: () => void;
}

const stepIcon = (status: PipelineStepStatus, isActive: boolean) => {
  if (status === 'Succeeded') return <CheckCircleIcon color="success" fontSize="small" />;
  if (status === 'Failed') return <ErrorIcon color="error" fontSize="small" />;
  if (status === 'Running' || isActive)
    return <CircularProgress size={16} thickness={5} />;
  if (status === 'Skipped') return <HourglassEmptyIcon color="disabled" fontSize="small" />;
  return <RadioButtonUncheckedIcon color="disabled" fontSize="small" />;
};

function StepCard({ step, isActive }: { step: PipelineRun['stepRuns'][0]; isActive: boolean }) {
  const duration = step.startedAt && step.completedAt
    ? Math.round((new Date(step.completedAt).getTime() - new Date(step.startedAt).getTime()) / 1000) + 's'
    : null;

  let parsedOutput: Record<string, unknown> | null = null;
  if (step.outputJson) {
    try { parsedOutput = JSON.parse(step.outputJson); } catch { /* ignore */ }
  }

  return (
    <Box sx={{
      p: 1.5,
      border: '1px solid',
      borderColor: isActive ? 'primary.main' : 'divider',
      borderRadius: 2,
      bgcolor: isActive ? 'primary.50' : 'background.paper',
      transition: 'all 0.3s'
    }}>
      <Stack direction="row" alignItems="center" spacing={1} mb={0.5}>
        {stepIcon(step.status, isActive)}
        <Typography variant="body2" fontWeight={600} sx={{ flex: 1 }}>{step.stepName}</Typography>
        {duration && <Chip label={duration} size="small" variant="outlined" />}
        <Chip
          label={step.status}
          size="small"
          color={
            step.status === 'Succeeded' ? 'success' :
            step.status === 'Failed' ? 'error' :
            step.status === 'Running' ? 'primary' : 'default'
          }
        />
      </Stack>

      {step.startedAt && (
        <Typography variant="caption" color="text.secondary" display="block">
          Started: {new Date(step.startedAt).toLocaleTimeString()}
          {step.completedAt && ` — Done: ${new Date(step.completedAt).toLocaleTimeString()}`}
        </Typography>
      )}

      {step.errorMessage && (
        <Typography variant="caption" color="error" display="block" mt={0.5}>
          {step.errorMessage}
        </Typography>
      )}

      {parsedOutput && (
        <Box mt={1}>
          <Typography variant="caption" color="text.secondary" fontWeight={600}>
            Output passed to next step:
          </Typography>
          <Box component="pre" sx={{
            mt: 0.5, p: 1, bgcolor: 'grey.900', color: 'grey.100',
            borderRadius: 1, fontSize: '0.7rem', overflow: 'auto',
            maxHeight: 100, fontFamily: 'monospace'
          }}>
            {JSON.stringify(parsedOutput, null, 2)}
          </Box>
        </Box>
      )}
    </Box>
  );
}

function RunDetail({ runId }: { runId: string }) {
  const { data: run } = useQuery({
    queryKey: ['pipelineRun', runId],
    queryFn: () => getPipelineRun(runId),
    refetchInterval: r => (r.state.data?.status === 'Running' || r.state.data?.status === 'Pending') ? 3000 : false,
  });

  if (!run) return <CircularProgress size={20} />;

  return (
    <Box>
      <Stack direction="row" spacing={1} alignItems="center" mb={2}>
        <Chip label={run.status} color={run.status === 'Succeeded' ? 'success' : run.status === 'Failed' ? 'error' : 'primary'} />
        <Typography variant="caption" color="text.secondary">
          {run.triggeredBy}
          {run.serviceBusMessageId && ` · via Service Bus`}
          {' · '}{new Date(run.startedAt).toLocaleString()}
        </Typography>
      </Stack>

      {/* Progress bar */}
      <Box sx={{ mb: 2, p: 1.5, bgcolor: 'grey.50', borderRadius: 2 }}>
        <Stack direction="row" justifyContent="space-between" mb={0.5}>
          <Typography variant="caption" fontWeight={600}>Progress</Typography>
          <Typography variant="caption">{Math.min(run.currentStepIndex + (run.status === 'Succeeded' ? 1 : 0), run.totalSteps)} / {run.totalSteps} steps</Typography>
        </Stack>
        <Box sx={{ height: 6, bgcolor: 'grey.200', borderRadius: 3, overflow: 'hidden' }}>
          <Box sx={{
            height: '100%',
            width: `${(Math.min(run.currentStepIndex + (run.status === 'Succeeded' ? 1 : 0), run.totalSteps) / run.totalSteps) * 100}%`,
            bgcolor: run.status === 'Failed' ? 'error.main' : 'primary.main',
            transition: 'width 0.5s ease',
          }} />
        </Box>
      </Box>

      <Stack spacing={1.5}>
        {run.stepRuns.map((step, i) => (
          <StepCard key={i} step={step} isActive={i === run.currentStepIndex && run.status === 'Running'} />
        ))}
      </Stack>
    </Box>
  );
}

export default function PipelineRunDrawer({ pipeline, onClose }: Props) {
  const { data: runs = [], isLoading } = useQuery({
    queryKey: ['pipelineRuns', pipeline?.id],
    queryFn: () => getPipelineRuns(pipeline!.id),
    enabled: Boolean(pipeline),
    refetchInterval: 5000,
  });

  return (
    <Drawer anchor="right" open={Boolean(pipeline)} onClose={onClose}
      PaperProps={{ sx: { width: 560 } }}>
      <Box sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>Pipeline Runs</Typography>
        <Typography variant="body2" color="text.secondary" gutterBottom>{pipeline?.name}</Typography>
        <Divider sx={{ my: 2 }} />

        {isLoading && <CircularProgress size={24} />}
        {!isLoading && runs.length === 0 && (
          <Typography color="text.secondary">No runs yet. Click "Run Pipeline" to start.</Typography>
        )}

        <Stack spacing={3}>
          {runs.map(run => (
            <Box key={run.id}>
              <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                Run {run.id.slice(0, 8)} — {new Date(run.startedAt).toLocaleString()}
              </Typography>
              <RunDetail runId={run.id} />
            </Box>
          ))}
        </Stack>
      </Box>
    </Drawer>
  );
}
