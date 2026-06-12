import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import DragHandleIcon from '@mui/icons-material/DragHandle';
import {
  Box,
  Chip,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
  arrayMove,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { JobDefinition } from '../../types';

interface StepItem {
  id: string;
  stepName: string;
  jobDefinitionId: string;
}

interface SortableStepProps {
  step: StepItem;
  index: number;
  job?: JobDefinition;
  onDelete: () => void;
  onChangeName: (name: string) => void;
  onChangeJob: (jobId: string) => void;
  availableJobs: JobDefinition[];
}

function SortableStep({ step, index, job, onDelete, onChangeName, onChangeJob, availableJobs }: SortableStepProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: step.id });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <Box ref={setNodeRef} style={style}>
      {/* Connector arrow above (not for first item) */}
      {index > 0 && (
        <Box sx={{ display: 'flex', justifyContent: 'center', my: 0.5 }}>
          <Box
            sx={{
              width: 2,
              height: 24,
              bgcolor: 'primary.light',
              position: 'relative',
              '&::after': {
                content: '"▼"',
                position: 'absolute',
                bottom: -8,
                left: '50%',
                transform: 'translateX(-50%)',
                fontSize: 10,
                color: 'primary.light',
              },
            }}
          />
        </Box>
      )}

      <Box
        sx={{
          border: '1px solid',
          borderColor: isDragging ? 'primary.main' : 'divider',
          borderRadius: 2,
          bgcolor: 'background.paper',
          boxShadow: isDragging ? 4 : 0,
          overflow: 'hidden',
        }}
      >
        <Stack direction="row" alignItems="center" spacing={1} sx={{ p: 1.5 }}>
          {/* Drag handle */}
          <Box
            {...attributes}
            {...listeners}
            sx={{ cursor: 'grab', color: 'text.disabled', display: 'flex', '&:active': { cursor: 'grabbing' } }}
          >
            <DragHandleIcon fontSize="small" />
          </Box>

          {/* Step badge */}
          <Box
            sx={{
              minWidth: 28,
              height: 28,
              borderRadius: '50%',
              bgcolor: 'primary.main',
              color: 'white',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: 12,
              fontWeight: 700,
              flexShrink: 0,
            }}
          >
            {index + 1}
          </Box>

          {/* Step name */}
          <TextField
            size="small"
            label="Step name"
            value={step.stepName}
            onChange={e => onChangeName(e.target.value)}
            sx={{ flex: 1 }}
          />

          {/* Job selector */}
          <TextField
            size="small"
            select
            label="Job"
            value={step.jobDefinitionId}
            onChange={e => onChangeJob(e.target.value)}
            sx={{ flex: 1.5 }}
          >
            {availableJobs.map(j => (
              <MenuItem key={j.id} value={j.id}>
                <Stack direction="row" spacing={1} alignItems="center">
                  <Chip
                    label={j.target}
                    size="small"
                    color={j.target === 'Batch' ? 'primary' : 'secondary'}
                    sx={{ height: 18, fontSize: 10 }}
                  />
                  <span>{j.name}</span>
                </Stack>
              </MenuItem>
            ))}
          </TextField>

          {/* Target badge if job selected */}
          {job && (
            <Chip
              label={job.target}
              size="small"
              color={job.target === 'Batch' ? 'primary' : 'secondary'}
              variant="outlined"
            />
          )}

          {/* Delete */}
          <Tooltip title="Remove step">
            <IconButton size="small" color="error" onClick={onDelete}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
    </Box>
  );
}

interface Props {
  steps: StepItem[];
  availableJobs: JobDefinition[];
  onChange: (steps: StepItem[]) => void;
}

export default function WorkflowCanvas({ steps, availableJobs, onChange }: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      const oldIndex = steps.findIndex(s => s.id === active.id);
      const newIndex = steps.findIndex(s => s.id === over.id);
      onChange(arrayMove(steps, oldIndex, newIndex));
    }
  };

  const addStep = (afterIndex?: number) => {
    const newStep: StepItem = {
      id: crypto.randomUUID(),
      stepName: `Step ${steps.length + 1}`,
      jobDefinitionId: availableJobs[0]?.id ?? '',
    };
    if (afterIndex !== undefined) {
      const updated = [...steps];
      updated.splice(afterIndex + 1, 0, newStep);
      onChange(updated);
    } else {
      onChange([...steps, newStep]);
    }
  };

  const deleteStep = (index: number) => {
    onChange(steps.filter((_, i) => i !== index));
  };

  const updateStep = (index: number, patch: Partial<StepItem>) => {
    const updated = [...steps];
    updated[index] = { ...updated[index], ...patch };
    onChange(updated);
  };

  return (
    <Box>
      {steps.length === 0 ? (
        <Box
          sx={{
            textAlign: 'center',
            py: 6,
            border: '2px dashed',
            borderColor: 'divider',
            borderRadius: 2,
          }}
        >
          <Typography variant="body2" color="text.secondary" gutterBottom>
            No steps yet. Add your first step to build the pipeline.
          </Typography>
          <IconButton
            color="primary"
            onClick={() => addStep()}
            sx={{
              mt: 1,
              border: '1px solid',
              borderColor: 'primary.main',
            }}
          >
            <AddIcon />
          </IconButton>
        </Box>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={steps.map(s => s.id)} strategy={verticalListSortingStrategy}>
            {steps.map((step, index) => (
              <SortableStep
                key={step.id}
                step={step}
                index={index}
                job={availableJobs.find(j => j.id === step.jobDefinitionId)}
                onDelete={() => deleteStep(index)}
                onChangeName={name => updateStep(index, { stepName: name })}
                onChangeJob={jobId => updateStep(index, { jobDefinitionId: jobId })}
                availableJobs={availableJobs}
              />
            ))}
          </SortableContext>
        </DndContext>
      )}

      {steps.length > 0 && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
          <Tooltip title="Add step">
            <IconButton
              color="primary"
              onClick={() => addStep()}
              sx={{
                border: '1px solid',
                borderColor: 'primary.main',
              }}
            >
              <AddIcon />
            </IconButton>
          </Tooltip>
        </Box>
      )}
    </Box>
  );
}
