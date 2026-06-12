import SendIcon from '@mui/icons-material/Send';
import {
  Box,
  Button,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import toast from 'react-hot-toast';
import { getJobs } from '../api/jobsApi';
import { getPipelines, getRecentMessages, sendServiceBusMessage } from '../api/pipelinesApi';
import type { SendMessagePayload } from '../types';

export default function ServiceBusPanel() {
  const queryClient = useQueryClient();
  const [action, setAction] = useState<'TriggerJob' | 'TriggerPipeline'>('TriggerJob');
  const [targetId, setTargetId] = useState('');
  const [payloadJson, setPayloadJson] = useState('{}');
  const [notes, setNotes] = useState('');
  const [jsonError, setJsonError] = useState('');

  const { data: batchJobs = [] } = useQuery({ queryKey: ['jobs', 'Batch'], queryFn: () => getJobs('Batch') });
  const { data: fnJobs = [] } = useQuery({ queryKey: ['jobs', 'Function'], queryFn: () => getJobs('Function') });
  const { data: pipelines = [] } = useQuery({ queryKey: ['pipelines'], queryFn: getPipelines });
  const { data: messages = [] } = useQuery({
    queryKey: ['sbMessages'],
    queryFn: getRecentMessages,
    refetchInterval: 3000,
  });

  const allJobs = [...batchJobs, ...fnJobs];
  const targets = action === 'TriggerJob'
    ? allJobs.map(j => ({ id: j.id, label: `[${j.target}] ${j.name}` }))
    : pipelines.map(p => ({ id: p.id, label: p.name }));

  const sendMutation = useMutation({
    mutationFn: (payload: SendMessagePayload) => sendServiceBusMessage(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sbMessages'] });
      queryClient.invalidateQueries({ queryKey: ['jobs'] });
      queryClient.invalidateQueries({ queryKey: ['pipelines'] });
      toast.success('Message sent to Service Bus');
      setPayloadJson('{}');
      setNotes('');
    },
    onError: () => toast.error('Failed to send message'),
  });

  const handleSend = () => {
    if (!targetId) { toast.error('Select a target'); return; }
    try { JSON.parse(payloadJson); } catch { toast.error('Invalid JSON payload'); return; }
    sendMutation.mutate({ action, targetId, payloadJson: payloadJson === '{}' ? undefined : payloadJson, notes: notes || undefined });
  };

  return (
    <Stack spacing={3}>
      {/* Compose panel */}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom fontWeight={600}>
          Send Service Bus Message
        </Typography>
        <Typography variant="body2" color="text.secondary" mb={2}>
          Messages are delivered to the job-triggers queue. The listener picks them up and triggers the target immediately.
        </Typography>

        <Stack spacing={2}>
          <FormControl size="small" fullWidth>
            <InputLabel>Action</InputLabel>
            <Select
              value={action}
              label="Action"
              onChange={e => { setAction(e.target.value as typeof action); setTargetId(''); }}
            >
              <MenuItem value="TriggerJob">TriggerJob — run a single job</MenuItem>
              <MenuItem value="TriggerPipeline">TriggerPipeline — run a full pipeline</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" fullWidth disabled={targets.length === 0}>
            <InputLabel>Target</InputLabel>
            <Select value={targetId} label="Target" onChange={e => setTargetId(e.target.value)}>
              {targets.map(t => <MenuItem key={t.id} value={t.id}>{t.label}</MenuItem>)}
            </Select>
          </FormControl>

          <TextField
            label="Extra Payload (JSON)"
            multiline rows={3}
            value={payloadJson}
            onChange={e => {
              setPayloadJson(e.target.value);
              try { JSON.parse(e.target.value); setJsonError(''); } catch { setJsonError('Invalid JSON'); }
            }}
            error={Boolean(jsonError)}
            helperText={jsonError || 'Optional JSON merged into the target\'s parameters'}
            InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
            fullWidth
          />

          <TextField
            label="Notes (optional)"
            value={notes}
            onChange={e => setNotes(e.target.value)}
            placeholder="e.g. triggered from demo for client review"
            fullWidth size="small"
          />

          <Button
            variant="contained"
            startIcon={<SendIcon />}
            onClick={handleSend}
            disabled={sendMutation.isPending || !targetId}
            sx={{ alignSelf: 'flex-start' }}
          >
            Send Message
          </Button>
        </Stack>
      </Paper>

      {/* Recent messages feed */}
      <Box>
        <Typography variant="subtitle1" fontWeight={600} gutterBottom>
          Recent Messages ({messages.length})
        </Typography>
        <Stack spacing={1}>
          {messages.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              No messages yet. Send one above to see it appear here.
            </Typography>
          )}
          {messages.map(msg => (
            <Paper key={msg.messageId} variant="outlined" sx={{ p: 1.5 }}>
              <Stack direction="row" spacing={1} alignItems="flex-start" justifyContent="space-between">
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack direction="row" spacing={1} alignItems="center" mb={0.5}>
                    <Chip
                      label={msg.action}
                      size="small"
                      color={msg.action === 'TriggerPipeline' ? 'secondary' : 'primary'}
                      variant="outlined"
                    />
                    <Typography variant="body2" fontWeight={500} noWrap>
                      {[...batchJobs, ...fnJobs].find(j => j.id === msg.targetId)?.name
                        || pipelines.find(p => p.id === msg.targetId)?.name
                        || msg.targetId.slice(0, 16) + '…'}
                    </Typography>
                  </Stack>
                  {msg.notes && (
                    <Typography variant="caption" color="text.secondary" display="block">{msg.notes}</Typography>
                  )}
                </Box>
                <Tooltip title={new Date(msg.enqueuedAt).toLocaleString()}>
                  <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
                    {new Date(msg.enqueuedAt).toLocaleTimeString()}
                  </Typography>
                </Tooltip>
              </Stack>
              <Typography variant="caption" color="text.disabled">
                ID: {msg.messageId.slice(0, 8)}… · by {msg.sentBy}
              </Typography>
            </Paper>
          ))}
        </Stack>
      </Box>
    </Stack>
  );
}
