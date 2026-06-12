import MoreVertIcon from '@mui/icons-material/MoreVert';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import {
  Box,
  Chip,
  CircularProgress,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Skeleton,
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
import { deleteJob, getJobs, setJobEnabled, triggerJob } from '../api/jobsApi';
import type { JobDefinition, JobTarget } from '../types';
import StatusBadge from './StatusBadge';

interface Props {
  target: JobTarget;
  onViewHistory: (job: JobDefinition) => void;
  onEdit: (job: JobDefinition) => void;
}

export default function JobsTable({ target, onViewHistory, onEdit }: Props) {
  const queryClient = useQueryClient();
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [activeJob, setActiveJob] = useState<JobDefinition | null>(null);

  const { data: jobs = [], isLoading } = useQuery({
    queryKey: ['jobs', target],
    queryFn: () => getJobs(target),
    refetchInterval: 5000,
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => setJobEnabled(id, enabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobs', target] }),
    onError: () => toast.error('Failed to update job state'),
  });

  const runMutation = useMutation({
    mutationFn: (id: string) => triggerJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs', target] });
      toast.success('Job triggered');
    },
    onError: () => toast.error('Failed to trigger job'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs', target] });
      toast.success('Job deleted');
    },
    onError: () => toast.error('Failed to delete job'),
  });

  const openMenu = (e: React.MouseEvent<HTMLElement>, job: JobDefinition) => {
    setMenuAnchor(e.currentTarget);
    setActiveJob(job);
  };
  const closeMenu = () => {
    setMenuAnchor(null);
  };

  if (isLoading) {
    return (
      <Box>
        {[...Array(3)].map((_, i) => (
          <Skeleton key={i} variant="rectangular" height={52} sx={{ mb: 1, borderRadius: 1 }} />
        ))}
      </Box>
    );
  }

  if (jobs.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h6" color="text.secondary" gutterBottom>
          No jobs yet
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Click "New Job" to create your first {target} job.
        </Typography>
      </Box>
    );
  }

  return (
    <>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'grey.50' }}>
              <TableCell><strong>Name</strong></TableCell>
              <TableCell><strong>Template</strong></TableCell>
              <TableCell><strong>Trigger</strong></TableCell>
              <TableCell align="center"><strong>Enabled</strong></TableCell>
              <TableCell><strong>Last Status</strong></TableCell>
              <TableCell><strong>Last Run</strong></TableCell>
              <TableCell align="center"><strong>Actions</strong></TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {jobs.map(job => {
              const triggerType = job.triggerConfig?.type ?? 'Manual';
              const triggerColor: 'default' | 'primary' | 'secondary' | 'info' | 'warning' =
                triggerType === 'Cron' ? 'primary' :
                triggerType === 'Http' ? 'info' :
                triggerType === 'ServiceBus' ? 'secondary' : 'default';
              return (
              <TableRow key={job.id} hover>
                <TableCell>
                  <Typography variant="body2" fontWeight={500}>{job.name}</Typography>
                  {triggerType === 'Cron' && (job.triggerConfig?.cronExpression ?? job.cronSchedule) && (
                    <Typography variant="caption" color="text.secondary">
                      {job.triggerConfig?.cronExpression ?? job.cronSchedule}
                    </Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Chip label={job.templateType ?? job.jobType} size="small" variant="outlined" />
                </TableCell>
                <TableCell>
                  <Chip label={triggerType} size="small" color={triggerColor} />
                </TableCell>
                <TableCell align="center">
                  <Switch
                    size="small"
                    checked={job.enabled}
                    onChange={e => toggleMutation.mutate({ id: job.id, enabled: e.target.checked })}
                    disabled={toggleMutation.isPending}
                  />
                </TableCell>
                <TableCell>
                  <StatusBadge status={job.lastRunStatus} />
                </TableCell>
                <TableCell>
                  {job.lastRunAt ? (
                    <Typography variant="caption">
                      {new Date(job.lastRunAt).toLocaleString()}
                    </Typography>
                  ) : (
                    <Typography variant="caption" color="text.secondary">Never</Typography>
                  )}
                </TableCell>
                <TableCell align="center">
                  <Tooltip title="Run Now">
                    <span>
                      <IconButton
                        size="small"
                        color="primary"
                        onClick={() => runMutation.mutate(job.id)}
                        disabled={runMutation.isPending}
                      >
                        {runMutation.isPending ? (
                          <CircularProgress size={16} />
                        ) : (
                          <PlayArrowIcon fontSize="small" />
                        )}
                      </IconButton>
                    </span>
                  </Tooltip>
                  <IconButton size="small" onClick={e => openMenu(e, job)}>
                    <MoreVertIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={closeMenu}>
        <MenuItem onClick={() => { onViewHistory(activeJob!); closeMenu(); }}>
          View History
        </MenuItem>
        <MenuItem onClick={() => { onEdit(activeJob!); closeMenu(); }}>
          Edit
        </MenuItem>
        <MenuItem
          sx={{ color: 'error.main' }}
          onClick={() => { deleteMutation.mutate(activeJob!.id); closeMenu(); }}
        >
          Delete
        </MenuItem>
      </Menu>
    </>
  );
}
