import {
  Box,
  FormControlLabel,
  MenuItem,
  Slider,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useState } from 'react';

interface FormProps {
  value: Record<string, unknown>;
  onChange: (v: Record<string, unknown>) => void;
}

function DataValidationForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Dataset"
        value={String(value.dataset ?? '')}
        onChange={e => onChange({ ...value, dataset: e.target.value })}
        placeholder="sales"
        fullWidth
      />
      <Box>
        <Typography variant="body2" gutterBottom>
          Quality Threshold: {Math.round(Number(value.threshold ?? 0.95) * 100)}%
        </Typography>
        <Slider
          value={Number(value.threshold ?? 0.95)}
          onChange={(_, v) => onChange({ ...value, threshold: v as number })}
          min={0}
          max={1}
          step={0.01}
          marks={[
            { value: 0, label: '0%' },
            { value: 0.5, label: '50%' },
            { value: 1, label: '100%' },
          ]}
        />
      </Box>
      <FormControlLabel
        control={
          <Switch
            checked={Boolean(value.notifyOnFailure ?? true)}
            onChange={e => onChange({ ...value, notifyOnFailure: e.target.checked })}
          />
        }
        label="Notify on Failure"
      />
    </Stack>
  );
}

function ReportGenerationForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Report Template"
        select
        value={String(value.template ?? 'weekly-summary')}
        onChange={e => onChange({ ...value, template: e.target.value })}
        fullWidth
      >
        {['weekly-summary', 'monthly-report', 'nightly', 'executive-dashboard'].map(t => (
          <MenuItem key={t} value={t}>{t}</MenuItem>
        ))}
      </TextField>
      <TextField
        label="Output Format"
        select
        value={String(value.format ?? 'pdf')}
        onChange={e => onChange({ ...value, format: e.target.value })}
        fullWidth
      >
        {['pdf', 'html', 'csv'].map(f => (
          <MenuItem key={f} value={f}>{f.toUpperCase()}</MenuItem>
        ))}
      </TextField>
      <TextField
        label="Recipients (comma-separated emails)"
        value={String(value.recipients ?? '')}
        onChange={e => onChange({ ...value, recipients: e.target.value })}
        placeholder="ops@example.com, team@example.com"
        fullWidth
      />
    </Stack>
  );
}

function DocumentProcessingForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Source Container"
        value={String(value.sourceContainer ?? '')}
        onChange={e => onChange({ ...value, sourceContainer: e.target.value })}
        placeholder="incoming-docs"
        fullWidth
      />
      <TextField
        label="Target Container"
        value={String(value.targetContainer ?? '')}
        onChange={e => onChange({ ...value, targetContainer: e.target.value })}
        placeholder="processed-docs"
        fullWidth
      />
      <FormControlLabel
        control={
          <Switch
            checked={Boolean(value.ocr ?? false)}
            onChange={e => onChange({ ...value, ocr: e.target.checked })}
          />
        }
        label="Enable OCR (Optical Character Recognition)"
      />
    </Stack>
  );
}

function StoredProcedureForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Key Vault Secret Name (connection string)"
        value={String(value.keyVaultSecretName ?? '')}
        onChange={e => onChange({ ...value, keyVaultSecretName: e.target.value })}
        helperText="Name of the secret in Key Vault containing the DB connection string"
        fullWidth
      />
      <TextField
        label="Stored Procedure Name"
        value={String(value.storedProcedureName ?? '')}
        onChange={e => onChange({ ...value, storedProcedureName: e.target.value })}
        placeholder="usp_ProcessData"
        fullWidth
      />
      <TextField
        label="Input Parameters (JSON)"
        multiline
        rows={3}
        value={String(value.inputParameters ?? '{}')}
        onChange={e => onChange({ ...value, inputParameters: e.target.value })}
        InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
        fullWidth
      />
      <TextField
        label="Command Timeout (seconds)"
        type="number"
        value={String(value.commandTimeoutSeconds ?? 30)}
        onChange={e => onChange({ ...value, commandTimeoutSeconds: Number(e.target.value) })}
        inputProps={{ min: 1, max: 3600 }}
        fullWidth
      />
    </Stack>
  );
}

function HttpCallForm({ value, onChange }: FormProps) {
  const method = String(value.method ?? 'GET');
  const authType = String(value.authType ?? 'None');
  return (
    <Stack spacing={2}>
      <TextField
        label="URL"
        value={String(value.url ?? '')}
        onChange={e => onChange({ ...value, url: e.target.value })}
        placeholder="https://api.example.com/endpoint"
        fullWidth
      />
      <TextField
        label="HTTP Method"
        select
        value={method}
        onChange={e => onChange({ ...value, method: e.target.value })}
        fullWidth
      >
        {['GET', 'POST', 'PUT', 'DELETE', 'PATCH'].map(m => (
          <MenuItem key={m} value={m}>{m}</MenuItem>
        ))}
      </TextField>
      <TextField
        label="Auth Type"
        select
        value={authType}
        onChange={e => onChange({ ...value, authType: e.target.value })}
        fullWidth
      >
        {['None', 'Bearer', 'ApiKey'].map(a => (
          <MenuItem key={a} value={a}>{a}</MenuItem>
        ))}
      </TextField>
      {authType !== 'None' && (
        <TextField
          label="Auth Key Vault Secret Name"
          value={String(value.authKeyVaultSecretName ?? '')}
          onChange={e => onChange({ ...value, authKeyVaultSecretName: e.target.value })}
          helperText="Name of the Key Vault secret containing the auth token"
          fullWidth
        />
      )}
      {method !== 'GET' && (
        <TextField
          label="Request Body (JSON)"
          multiline
          rows={3}
          value={String(value.body ?? '')}
          onChange={e => onChange({ ...value, body: e.target.value })}
          InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
          fullWidth
        />
      )}
      <Stack direction="row" spacing={2}>
        <TextField
          label="Retry Count"
          type="number"
          value={String(value.retryCount ?? 1)}
          onChange={e => onChange({ ...value, retryCount: Math.min(5, Math.max(0, Number(e.target.value))) })}
          inputProps={{ min: 0, max: 5 }}
          fullWidth
        />
        <TextField
          label="Timeout (seconds)"
          type="number"
          value={String(value.timeoutSeconds ?? 30)}
          onChange={e => onChange({ ...value, timeoutSeconds: Number(e.target.value) })}
          inputProps={{ min: 1, max: 300 }}
          fullWidth
        />
      </Stack>
    </Stack>
  );
}

function ServiceBusSendForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Connection Key Vault Secret Name"
        value={String(value.connectionKeyVaultSecretName ?? '')}
        onChange={e => onChange({ ...value, connectionKeyVaultSecretName: e.target.value })}
        helperText="Name of the Key Vault secret containing the Service Bus connection string"
        fullWidth
      />
      <TextField
        label="Queue or Topic Name"
        value={String(value.queueOrTopicName ?? '')}
        onChange={e => onChange({ ...value, queueOrTopicName: e.target.value })}
        placeholder="outgoing-messages"
        fullWidth
      />
      <TextField
        label="Message Body"
        multiline
        rows={4}
        value={String(value.messageBody ?? '{}')}
        onChange={e => onChange({ ...value, messageBody: e.target.value })}
        InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
        fullWidth
      />
      <TextField
        label="Message Properties (JSON)"
        multiline
        rows={2}
        value={String(value.messageProperties ?? '{}')}
        onChange={e => onChange({ ...value, messageProperties: e.target.value })}
        InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
        helperText="Additional message properties as key-value pairs"
        fullWidth
      />
    </Stack>
  );
}

function BlobOperationForm({ value, onChange }: FormProps) {
  return (
    <Stack spacing={2}>
      <TextField
        label="Container Name"
        value={String(value.containerName ?? '')}
        onChange={e => onChange({ ...value, containerName: e.target.value })}
        placeholder="data"
        fullWidth
      />
      <TextField
        label="Folder Path"
        value={String(value.folderPath ?? '/')}
        onChange={e => onChange({ ...value, folderPath: e.target.value })}
        placeholder="/archive/2024"
        fullWidth
      />
      <TextField
        label="File Extension Filter"
        value={String(value.fileExtension ?? '*')}
        onChange={e => onChange({ ...value, fileExtension: e.target.value })}
        placeholder="csv"
        helperText="Leave * for all files"
        fullWidth
      />
      <TextField
        label="Operation"
        select
        value={String(value.operation ?? 'Archive')}
        onChange={e => onChange({ ...value, operation: e.target.value })}
        fullWidth
      >
        {['Archive', 'Delete'].map(o => (
          <MenuItem key={o} value={o}>{o}</MenuItem>
        ))}
      </TextField>
      <TextField
        label="Retention Days"
        type="number"
        value={String(value.retentionDays ?? 30)}
        onChange={e => onChange({ ...value, retentionDays: Number(e.target.value) })}
        helperText="Files older than this many days will be processed"
        inputProps={{ min: 1 }}
        fullWidth
      />
    </Stack>
  );
}

interface ParameterFormProps {
  templateType: string;
  value: string;
  onChange: (json: string) => void;
}

export default function ParameterForm({ templateType, value, onChange }: ParameterFormProps) {
  const [params, setParams] = useState<Record<string, unknown>>({});

  useEffect(() => {
    try {
      setParams(JSON.parse(value) as Record<string, unknown>);
    } catch {
      setParams({});
    }
  }, [value]);

  const handleChange = (newParams: Record<string, unknown>) => {
    setParams(newParams);
    onChange(JSON.stringify(newParams));
  };

  const formProps: FormProps = { value: params, onChange: handleChange };

  switch (templateType) {
    case 'DataValidation':
      return <DataValidationForm {...formProps} />;
    case 'ReportGeneration':
      return <ReportGenerationForm {...formProps} />;
    case 'DocumentProcessing':
      return <DocumentProcessingForm {...formProps} />;
    case 'StoredProcedure':
      return <StoredProcedureForm {...formProps} />;
    case 'HttpCall':
      return <HttpCallForm {...formProps} />;
    case 'ServiceBusSend':
      return <ServiceBusSendForm {...formProps} />;
    case 'BlobOperation':
      return <BlobOperationForm {...formProps} />;
    default:
      return (
        <TextField
          label="Parameters (JSON)"
          multiline
          rows={6}
          value={value}
          onChange={e => onChange(e.target.value)}
          InputProps={{ sx: { fontFamily: 'monospace', fontSize: '0.85rem' } }}
          fullWidth
        />
      );
  }
}
