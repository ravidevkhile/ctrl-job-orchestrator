import { Box, Typography } from '@mui/material';
import TemplateSelector from '../components/wizard/TemplateSelector';
import { useState } from 'react';

export default function TemplatesTab() {
  const [selected, setSelected] = useState('DataValidation');
  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Job Templates
      </Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Browse available job templates. Select a template when creating a new job to get a pre-configured parameter form.
      </Typography>
      <TemplateSelector selected={selected} onSelect={setSelected} />
    </Box>
  );
}
