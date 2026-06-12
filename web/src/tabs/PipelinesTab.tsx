import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import HistoryIcon from '@mui/icons-material/History';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import {
  Box,
  Button,
  Chip,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import toast from 'react-hot-toast';
import {
  createPipeline,
  deletePipeline,
  getPipelines,
  setPipelineEnabled,
  triggerPipeline,
  updatePipeline,
} from '../api/pipelinesApi';
import PipelineFormDialog from '../components/PipelineFormDialog';
import PipelineRunDrawer from '../components/PipelineRunDrawer';
import StatusBadge from '../components/StatusBadge';
import type { CreatePipelinePayload, PipelineDefinition, PipelineStatus } from '../types';

const pipelineStatusMap: Record<PipelineStatus, Parameters<typeof StatusBadge>[0]['status']> = {
  Pending: 'Pending', Running: 'Running', Succeeded: 'Succeeded', Failed: 'Failed', Cancelled: 'Cancelled',
};

function PipelineStatusBadge({ status }: { status?: PipelineStatus }) {
  return <StatusBadge status={status ? pipelineStatusMap[status] : undefined} />;
}

export default function PipelinesTab() {
  const queryClient = useQueryClient();
  const [formOpen, setFormOpen] = useState(false);
  const [editPipeline, setEditPipeline] = useState<PipelineDefinition | null>(null);
  const [historyPipeline, setHistoryPipeline] = useState<PipelineDefinition | null>(null);

  const { data: pipelines = [], isLoading } = useQuery({
    queryKey: ['pipelines'],
    queryFn: getPipelines,
    refetchInterval: 5000,
  });

  const createMutation = useMutation({
    mutationFn: createPipeline,
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['pipelines'] }); toast.success('Pipeline created'); },
    onError: () => toast.error('Failed to create pipeline'),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Partial<CreatePipelinePayload> }) => updatePipeline(id, payload),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['pipelines'] }); toast.success('Pipeline updated'); },
    onError: () => toast.error('Failed to update pipeline'),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => setPipelineEnabled(id, enabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pipelines'] }),
  });

  const runMutation = useMutation({
    mutationFn: (id: string) => triggerPipeline(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['pipelines'] }); toast.success('Pipeline started'); },
    onError: () => toast.error('Failed to start pipeline'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deletePipeline(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['pipelines'] }); toast.success('Pipeline deleted'); },
  });

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={3}>
        <Box>
          <Typography variant="h5" fontWeight={700}>Job Pipelines</Typography>
          <Typography variant="body2" color="text.secondary">
            Chain jobs sequentially — each step&apos;s output feeds into the next step as parameters.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setFormOpen(true)}>
          New Pipeline
        </Button>
      </Stack>

      {isLoading ? (
        [...Array(2)].map((_, i) => <Skeleton key={i} variant="rectangular" height={52} sx={{ mb: 1, borderRadius: 1 }} />)
      ) : pipelines.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h6" color="text.secondary" gutterBottom>No pipelines yet</Typography>
          <Typography variant="body2" color="text.secondary">
            Click "New Pipeline" to chain your first sequence of jobs.
          </Typography>
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: 'grey.50' }}>
                <TableCell><strong>Name</strong></TableCell>
                <TableCell><strong>Steps</strong></TableCell>
                <TableCell align="center"><strong>Enabled</strong></TableCell>
                <TableCell><strong>Last Status</strong></TableCell>
                <TableCell><strong>Last Run</strong></TableCell>
                <TableCell align="center"><strong>Actions</strong></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {pipelines.map(p => (
                <TableRow key={p.id} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={500}>{p.name}</Typography>
                    {p.description && (
                      <Typography variant="caption" color="text.secondary">{p.description}</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} flexWrap="wrap">
                      {p.steps.map((s, i) => (
                        <Chip key={i} label={`${i + 1}. ${s.stepName}`} size="small" variant="outlined" />
                      ))}
                    </Stack>
                  </TableCell>
                  <TableCell align="center">
                    <Switch
                      size="small"
                      checked={p.enabled}
                      onChange={e => toggleMutation.mutate({ id: p.id, enabled: e.target.checked })}
                    />
                  </TableCell>
                  <TableCell><PipelineStatusBadge status={p.lastRunStatus} /></TableCell>
                  <TableCell>
                    {p.lastRunAt
                      ? <Typography variant="caption">{new Date(p.lastRunAt).toLocaleString()}</Typography>
                      : <Typography variant="caption" color="text.secondary">Never</Typography>}
                  </TableCell>
                  <TableCell align="center">
                    <Tooltip title="Run Pipeline">
                      <IconButton size="small" color="primary" onClick={() => runMutation.mutate(p.id)}>
                        <PlayArrowIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Edit">
                      <IconButton size="small" onClick={() => { setEditPipeline(p); setFormOpen(true); }}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="View Run History">
                      <IconButton size="small" onClick={() => setHistoryPipeline(p)}>
                        <HistoryIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Delete">
                      <IconButton size="small" color="error" onClick={() => deleteMutation.mutate(p.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <PipelineFormDialog
        key={editPipeline?.id ?? 'new'}
        open={formOpen}
        pipeline={editPipeline ?? undefined}
        onClose={() => { setFormOpen(false); setEditPipeline(null); }}
        onSubmit={payload =>
          editPipeline
            ? updateMutation.mutate({ id: editPipeline.id, payload })
            : createMutation.mutate(payload)
        }
      />

      <PipelineRunDrawer
        pipeline={historyPipeline}
        onClose={() => setHistoryPipeline(null)}
      />
    </Box>
  );
}
