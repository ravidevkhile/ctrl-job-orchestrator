import { Box, Stack, Typography } from '@mui/material';
import ServiceBusPanel from '../components/ServiceBusPanel';

export default function ServiceBusTab() {
  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={3}>
        <Box>
          <Typography variant="h5" fontWeight={700}>Service Bus</Typography>
          <Typography variant="body2" color="text.secondary">
            Send a message to the job-triggers queue to trigger a job or pipeline asynchronously.
          </Typography>
        </Box>
      </Stack>
      <ServiceBusPanel />
    </Box>
  );
}
