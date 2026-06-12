import AssessmentIcon from '@mui/icons-material/Assessment';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import DescriptionIcon from '@mui/icons-material/Description';
import EmailIcon from '@mui/icons-material/Email';
import HttpIcon from '@mui/icons-material/Http';
import StorageIcon from '@mui/icons-material/Storage';
import {
  Box,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Grid,
  Stack,
  Typography,
} from '@mui/material';

interface TemplateInfo {
  type: string;
  icon: React.ReactNode;
  label: string;
  description: string;
  color: string;
  tier: 'Standard' | 'Enterprise';
}

const templates: TemplateInfo[] = [
  {
    type: 'DataValidation',
    icon: <CheckCircleIcon sx={{ fontSize: 36 }} />,
    label: 'Data Validation',
    description: 'Validate datasets against quality thresholds. Supports configurable pass/fail criteria and notifications.',
    color: '#0078d4',
    tier: 'Standard',
  },
  {
    type: 'ReportGeneration',
    icon: <AssessmentIcon sx={{ fontSize: 36 }} />,
    label: 'Report Generation',
    description: 'Generate and distribute scheduled reports in PDF, HTML, or CSV format to configured recipients.',
    color: '#107C10',
    tier: 'Standard',
  },
  {
    type: 'DocumentProcessing',
    icon: <DescriptionIcon sx={{ fontSize: 36 }} />,
    label: 'Document Processing',
    description: 'Process documents with OCR, extraction, and transformation between blob storage containers.',
    color: '#D83B01',
    tier: 'Standard',
  },
  {
    type: 'StoredProcedure',
    icon: <StorageIcon sx={{ fontSize: 36 }} />,
    label: 'Stored Procedure',
    description: 'Execute SQL stored procedures with Key Vault-managed connection strings and configurable timeouts.',
    color: '#8764B8',
    tier: 'Enterprise',
  },
  {
    type: 'HttpCall',
    icon: <HttpIcon sx={{ fontSize: 36 }} />,
    label: 'HTTP Call',
    description: 'Make authenticated HTTP requests to any API. Supports Bearer tokens, retries, and custom headers.',
    color: '#00B4D8',
    tier: 'Standard',
  },
  {
    type: 'ServiceBusSend',
    icon: <EmailIcon sx={{ fontSize: 36 }} />,
    label: 'Service Bus Send',
    description: 'Send messages to Azure Service Bus queues or topics with Key Vault-managed connection strings.',
    color: '#0078d4',
    tier: 'Enterprise',
  },
  {
    type: 'BlobOperation',
    icon: <CloudUploadIcon sx={{ fontSize: 36 }} />,
    label: 'Blob Operation',
    description: 'Archive or delete blob files based on age and pattern filters. Frees storage automatically.',
    color: '#605e5c',
    tier: 'Standard',
  },
];

interface Props {
  selected: string;
  onSelect: (type: string) => void;
}

export default function TemplateSelector({ selected, onSelect }: Props) {
  return (
    <Box>
      <Typography variant="body2" color="text.secondary" mb={2}>
        Choose a template to determine what this job does and what parameters it needs.
      </Typography>
      <Grid container spacing={2}>
        {templates.map(t => {
          const isSelected = selected === t.type;
          return (
            <Grid item xs={12} sm={6} md={4} key={t.type}>
              <Card
                sx={{
                  height: '100%',
                  border: isSelected ? `2px solid ${t.color}` : '1px solid #e1dfdd',
                  boxShadow: isSelected ? `0 0 0 3px ${t.color}22` : 'none',
                  transition: 'all 0.15s ease',
                }}
              >
                <CardActionArea
                  onClick={() => onSelect(t.type)}
                  sx={{ height: '100%', p: 0.5 }}
                >
                  <CardContent>
                    <Box sx={{ color: isSelected ? t.color : 'text.secondary', mb: 1 }}>{t.icon}</Box>
                    <Stack direction="row" alignItems="center" spacing={1} mb={0.5}>
                      <Typography variant="subtitle2" fontWeight={700}>
                        {t.label}
                      </Typography>
                      <Chip
                        label={t.tier}
                        size="small"
                        variant="outlined"
                        color={t.tier === 'Enterprise' ? 'secondary' : 'default'}
                        sx={{ height: 18, fontSize: 10 }}
                      />
                    </Stack>
                    <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.4 }}>
                      {t.description}
                    </Typography>
                  </CardContent>
                </CardActionArea>
              </Card>
            </Grid>
          );
        })}
      </Grid>
    </Box>
  );
}
