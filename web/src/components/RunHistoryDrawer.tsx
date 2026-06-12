import {
  Box,
  Button,
  CircularProgress,
  Divider,
  Drawer,
  Link,
  Stack,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getJobRuns, rerunRun } from '../api/jobsApi';
import type { JobDefinition } from '../types';
import StatusBadge from './StatusBadge';
import toast from 'react-hot-toast';

interface Props {
  job: JobDefinition | null;
  onClose: () => void;
  onRerun?: () => void;
}

function formatDuration(start: string, end?: string): string {
  const ms = (end ? new Date(end) : new Date()).getTime() - new Date(start).getTime();
  if (ms < 1000) return `${ms}ms`;
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s`;
  return `${Math.floor(s / 60)}m ${s % 60}s`;
}

export default function RunHistoryDrawer({ job, onClose, onRerun }: Props) {
  const { data: runs = [], isLoading } = useQuery({
    queryKey: ['runs', job?.id],
    queryFn: () => getJobRuns(job!.id),
    enabled: Boolean(job),
    refetchInterval: 5000,
  });

  const handleRerun = async (runId: string) => {
    try {
      await rerunRun(runId);
      toast.success('Rerun started');
      onRerun?.();
    } catch {
      toast.error('Failed to rerun');
    }
  };

  return (
    <Drawer anchor="right" open={Boolean(job)} onClose={onClose} PaperProps={{ sx: { width: 520 } }}>
      <Box sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Run History
        </Typography>
        <Typography variant="body2" color="text.secondary" gutterBottom>
          {job?.name}
        </Typography>
        <Divider sx={{ my: 2 }} />

        {isLoading && <CircularProgress size={24} />}

        {!isLoading && runs.length === 0 && (
          <Typography color="text.secondary">No runs yet.</Typography>
        )}

        <Stack spacing={2}>
          {runs.map(run => (
            <Box key={run.id} sx={{ p: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1}>
                <StatusBadge status={run.status} />
                <Typography variant="caption" color="text.secondary">
                  {run.triggeredBy} · {run.startedByUser}
                </Typography>
              </Stack>

              <Typography variant="caption" display="block" color="text.secondary">
                Started: {new Date(run.startedAt).toLocaleString()}
              </Typography>
              {run.completedAt && (
                <Typography variant="caption" display="block" color="text.secondary">
                  Completed: {new Date(run.completedAt).toLocaleString()}
                  {' '}({formatDuration(run.startedAt, run.completedAt)})
                </Typography>
              )}
              {run.status === 'Running' && (
                <Typography variant="caption" display="block" color="primary">
                  Running for {formatDuration(run.startedAt)}…
                </Typography>
              )}

              {run.log && (
                <Box
                  component="pre"
                  sx={{
                    mt: 1,
                    p: 1,
                    bgcolor: 'grey.900',
                    color: 'grey.100',
                    borderRadius: 1,
                    fontSize: '0.72rem',
                    overflow: 'auto',
                    maxHeight: 120,
                    fontFamily: 'monospace',
                  }}
                >
                  {run.log}
                </Box>
              )}

              {run.errorMessage && (
                <Typography variant="caption" color="error" display="block" mt={0.5}>
                  Error: {run.errorMessage}
                </Typography>
              )}

              <Stack direction="row" spacing={1} mt={1} alignItems="center">
                {run.outputLocation && (
                  <Link href={run.outputLocation} target="_blank" variant="caption">
                    View Output
                  </Link>
                )}
                <Button size="small" onClick={() => handleRerun(run.id)}>
                  Rerun
                </Button>
              </Stack>
            </Box>
          ))}
        </Stack>
      </Box>
    </Drawer>
  );
}
