import CloudIcon from '@mui/icons-material/Cloud';
import FunctionsIcon from '@mui/icons-material/Functions';
import {
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Step,
  StepLabel,
  Stepper,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import type { CreateJobPayload, JobDefinition, JobTarget, TriggerConfig } from '../../types';
import ParameterForm from './ParameterForms';
import TemplateSelector from './TemplateSelector';
import TriggerConfigStep from './TriggerConfigStep';

const STEPS = ['Choose Target', 'Select Template', 'Configure Trigger', 'Parameters & Review'];

interface TargetCard {
  target: JobTarget;
  icon: React.ReactNode;
  label: string;
  description: string;
  color: string;
}

const targetCards: TargetCard[] = [
  {
    target: 'Batch',
    icon: <CloudIcon sx={{ fontSize: 48 }} />,
    label: 'Azure Batch',
    description: 'Heavy compute workloads fanned out across auto-scaling nodes. Best for data processing, ML training, and long-running tasks.',
    color: '#0078d4',
  },
  {
    target: 'Function',
    icon: <FunctionsIcon sx={{ fontSize: 48 }} />,
    label: 'Azure Functions',
    description: 'Lightweight, fast, serverless job invocations. Best for HTTP calls, Service Bus interactions, and short-lived tasks.',
    color: '#8764B8',
  },
];

interface Props {
  open: boolean;
  job?: JobDefinition;
  onClose: () => void;
  onSubmit: (payload: CreateJobPayload) => void;
}

export default function JobWizard({ open, job, onClose, onSubmit }: Props) {
  const [step, setStep] = useState(0);
  const [target, setTarget] = useState<JobTarget>(job?.target ?? 'Batch');
  const [templateType, setTemplateType] = useState(job?.templateType ?? job?.jobType ?? 'DataValidation');
  const [triggerConfig, setTriggerConfig] = useState<TriggerConfig>(
    job?.triggerConfig ?? { type: 'Manual', timeZone: 'UTC' }
  );
  const [name, setName] = useState(job?.name ?? '');
  const [parametersJson, setParametersJson] = useState(job?.parametersJson ?? '{}');
  const [enabled, setEnabled] = useState(job?.enabled ?? true);

  const canGoNext = () => {
    if (step === 0) return true;
    if (step === 1) return !!templateType;
    if (step === 2) return true;
    if (step === 3) return name.trim().length > 0;
    return true;
  };

  const handleNext = () => setStep(s => Math.min(s + 1, STEPS.length - 1));
  const handleBack = () => setStep(s => Math.max(s - 1, 0));

  const handleSubmit = () => {
    onSubmit({
      name: name.trim(),
      target,
      jobType: templateType,
      templateType,
      parametersJson,
      triggerConfig,
      cronSchedule:
        triggerConfig.type === 'Cron' ? (triggerConfig.cronExpression ?? undefined) : undefined,
      enabled,
    });
    onClose();
    // Reset state
    setStep(0);
    setTarget('Batch');
    setTemplateType('DataValidation');
    setTriggerConfig({ type: 'Manual', timeZone: 'UTC' });
    setName('');
    setParametersJson('{}');
    setEnabled(true);
  };

  const handleClose = () => {
    onClose();
    setStep(0);
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        {job ? 'Edit Job' : 'Create New Job'}
        <Typography variant="body2" color="text.secondary" mt={0.5}>
          {STEPS[step]}
        </Typography>
      </DialogTitle>

      <Box sx={{ px: 3, pb: 1 }}>
        <Stepper activeStep={step} alternativeLabel>
          {STEPS.map(label => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>
      </Box>

      <Divider />

      <DialogContent sx={{ minHeight: 400, py: 3 }}>
        {/* Step 0: Choose Target */}
        {step === 0 && (
          <Box>
            <Typography variant="body2" color="text.secondary" mb={3}>
              Select where this job will execute.
            </Typography>
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              {targetCards.map(tc => {
                const selected = target === tc.target;
                return (
                  <Card
                    key={tc.target}
                    sx={{
                      flex: '1 1 240px',
                      border: selected ? `2px solid ${tc.color}` : '1px solid #e1dfdd',
                      boxShadow: selected ? `0 0 0 3px ${tc.color}22` : 'none',
                      transition: 'all 0.15s ease',
                    }}
                  >
                    <CardActionArea
                      onClick={() => setTarget(tc.target)}
                      sx={{ p: 3, height: '100%' }}
                    >
                      <CardContent sx={{ textAlign: 'center' }}>
                        <Box sx={{ color: selected ? tc.color : 'text.secondary', mb: 2 }}>
                          {tc.icon}
                        </Box>
                        <Typography variant="h6" gutterBottom>
                          {tc.label}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {tc.description}
                        </Typography>
                      </CardContent>
                    </CardActionArea>
                  </Card>
                );
              })}
            </Box>
          </Box>
        )}

        {/* Step 1: Template */}
        {step === 1 && (
          <TemplateSelector selected={templateType} onSelect={setTemplateType} />
        )}

        {/* Step 2: Trigger */}
        {step === 2 && (
          <TriggerConfigStep
            jobId={job?.id}
            value={triggerConfig}
            onChange={setTriggerConfig}
          />
        )}

        {/* Step 3: Parameters + Review */}
        {step === 3 && (
          <Box>
            <TextField
              label="Job Name"
              required
              value={name}
              onChange={e => setName(e.target.value)}
              fullWidth
              sx={{ mb: 3 }}
            />

            <Typography variant="subtitle2" gutterBottom>
              Parameters for {templateType}
            </Typography>
            <ParameterForm
              templateType={templateType}
              value={parametersJson}
              onChange={setParametersJson}
            />

            <Box sx={{ mt: 3 }}>
              <FormControlLabel
                control={<Switch checked={enabled} onChange={e => setEnabled(e.target.checked)} />}
                label="Enabled"
              />
            </Box>

            {/* Summary */}
            <Box
              sx={{
                mt: 3,
                p: 2,
                bgcolor: 'grey.50',
                borderRadius: 2,
                border: '1px solid',
                borderColor: 'divider',
              }}
            >
              <Typography variant="subtitle2" gutterBottom>
                Summary
              </Typography>
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                <Chip label={`Target: ${target}`} size="small" color="primary" />
                <Chip label={`Template: ${templateType}`} size="small" color="secondary" />
                <Chip label={`Trigger: ${triggerConfig.type}`} size="small" />
                {triggerConfig.type === 'Cron' && triggerConfig.cronExpression && (
                  <Chip label={triggerConfig.cronExpression} size="small" variant="outlined" />
                )}
                <Chip
                  label={enabled ? 'Enabled' : 'Disabled'}
                  size="small"
                  color={enabled ? 'success' : 'default'}
                />
              </Box>
            </Box>
          </Box>
        )}
      </DialogContent>

      <Divider />
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} color="inherit">
          Cancel
        </Button>
        <Box sx={{ flex: 1 }} />
        {step > 0 && (
          <Button onClick={handleBack} variant="outlined">
            Back
          </Button>
        )}
        {step < STEPS.length - 1 ? (
          <Button onClick={handleNext} variant="contained" disabled={!canGoNext()}>
            Next
          </Button>
        ) : (
          <Button onClick={handleSubmit} variant="contained" disabled={!name.trim()}>
            {job ? 'Save Changes' : 'Create Job'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
