import { Chip } from '@mui/material';
import type { JobStatus } from '../types';

interface Props {
  status: JobStatus | undefined;
  size?: 'small' | 'medium';
}

const config: Record<JobStatus, { label: string; color: 'default' | 'primary' | 'success' | 'error' | 'warning' }> = {
  Pending: { label: 'Pending', color: 'default' },
  Running: { label: 'Running', color: 'primary' },
  Succeeded: { label: 'Succeeded', color: 'success' },
  Failed: { label: 'Failed', color: 'error' },
  Cancelled: { label: 'Cancelled', color: 'warning' },
};

export default function StatusBadge({ status, size = 'small' }: Props) {
  if (!status) return <Chip label="—" size={size} />;
  const entry = config[status as JobStatus];
  if (!entry) return <Chip label={String(status)} size={size} />;
  const { label, color } = entry;

  return (
    <Chip
      label={label}
      color={color}
      size={size}
      sx={
        status === 'Running'
          ? {
              animation: 'pulse 1.5s infinite',
              '@keyframes pulse': {
                '0%, 100%': { opacity: 1 },
                '50%': { opacity: 0.55 },
              },
            }
          : undefined
      }
    />
  );
}
