import AddIcon from '@mui/icons-material/Add';
import { Box, Button, Stack, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import toast from 'react-hot-toast';
import { createJob, updateJob } from '../api/jobsApi';
import JobsTable from '../components/JobsTable';
import RunHistoryDrawer from '../components/RunHistoryDrawer';
import JobWizard from '../components/wizard/JobWizard';
import type { CreateJobPayload, JobDefinition } from '../types';

export default function FunctionsTab() {
  const queryClient = useQueryClient();
  const [wizardOpen, setWizardOpen] = useState(false);
  const [editJob, setEditJob] = useState<JobDefinition | null>(null);
  const [historyJob, setHistoryJob] = useState<JobDefinition | null>(null);

  const createMutation = useMutation({
    mutationFn: createJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs', 'Function'] });
      toast.success('Job created');
    },
    onError: () => toast.error('Failed to create job'),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateJobPayload }) =>
      updateJob(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs', 'Function'] });
      toast.success('Job updated');
    },
    onError: () => toast.error('Failed to update job'),
  });

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={3}>
        <Box>
          <Typography variant="h5" fontWeight={700}>Azure Functions Jobs</Typography>
          <Typography variant="body2" color="text.secondary">
            Lightweight, fast, serverless job invocations
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setWizardOpen(true)}>
          New Job
        </Button>
      </Stack>

      <JobsTable
        target="Function"
        onViewHistory={setHistoryJob}
        onEdit={job => { setEditJob(job); setWizardOpen(true); }}
      />

      <JobWizard
        open={wizardOpen}
        job={editJob ?? undefined}
        onClose={() => { setWizardOpen(false); setEditJob(null); }}
        onSubmit={payload =>
          editJob
            ? updateMutation.mutate({ id: editJob.id, payload })
            : createMutation.mutate(payload)
        }
      />

      <RunHistoryDrawer
        job={historyJob}
        onClose={() => setHistoryJob(null)}
        onRerun={() => queryClient.invalidateQueries({ queryKey: ['jobs', 'Function'] })}
      />
    </Box>
  );
}
