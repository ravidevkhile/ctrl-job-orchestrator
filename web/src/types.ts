// ---- Existing types -------------------------------------------------------

export type JobTarget = 'Batch' | 'Function';
export type JobStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';
export type TriggerType = 'Manual' | 'Rerun' | 'Scheduled';

export type TriggerConfigType = 'Manual' | 'Cron' | 'Http' | 'ServiceBus';

export interface TriggerConfig {
  type: TriggerConfigType;
  cronExpression?: string;
  timeZone?: string;
  httpToken?: string;
  serviceBusKeyVaultSecretName?: string;
  serviceBusQueueName?: string;
  serviceBusMessageFilter?: string;
}

export interface JobDefinition {
  id: string;
  name: string;
  target: JobTarget;
  jobType: string;
  parametersJson: string;
  cronSchedule?: string;
  enabled: boolean;
  createdBy: string;
  createdAt: string;
  lastRunId?: string;
  lastRunStatus?: JobStatus;
  lastRunAt?: string;
  triggerConfig?: TriggerConfig;
  templateType?: string;
}

export interface JobRun {
  id: string;
  jobDefinitionId: string;
  target: JobTarget;
  status: JobStatus;
  triggeredBy: TriggerType;
  startedByUser: string;
  startedAt: string;
  completedAt?: string;
  externalId?: string;
  outputLocation?: string;
  log?: string;
  errorMessage?: string;
  parametersJson: string;
  jobType: string;
  pipelineRunId?: string;
  pipelineStepIndex?: number;
  structuredOutputJson?: string;
}

export interface CreateJobPayload {
  name: string;
  target: JobTarget;
  jobType: string;
  parametersJson: string;
  cronSchedule?: string;
  enabled: boolean;
  triggerConfig?: TriggerConfig;
  templateType?: string;
}

export interface UpdateJobPayload {
  name?: string;
  jobType?: string;
  parametersJson?: string;
  cronSchedule?: string;
  enabled?: boolean;
  triggerConfig?: TriggerConfig;
  templateType?: string;
}

export const ALL_TEMPLATE_TYPES = [
  'DataValidation',
  'ReportGeneration',
  'DocumentProcessing',
  'StoredProcedure',
  'HttpCall',
  'ServiceBusSend',
  'BlobOperation',
] as const;

export type TemplateName = typeof ALL_TEMPLATE_TYPES[number];

export const JOB_TYPES: Record<JobTarget, string[]> = {
  Batch: [...ALL_TEMPLATE_TYPES],
  Function: [...ALL_TEMPLATE_TYPES],
};

// ---- Pipeline types -------------------------------------------------------

export type PipelineStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';
export type PipelineStepStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Skipped';

export interface PipelineStep {
  order: number;
  stepName: string;
  jobDefinitionId: string;
  outputMappingJson?: string;
}

export interface PipelineDefinition {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
  steps: PipelineStep[];
  createdBy: string;
  createdAt: string;
  lastRunId?: string;
  lastRunStatus?: PipelineStatus;
  lastRunAt?: string;
}

export interface PipelineStepRun {
  stepIndex: number;
  stepName: string;
  jobDefinitionId: string;
  jobRunId?: string;
  status: PipelineStepStatus;
  outputJson?: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}

export interface PipelineRun {
  id: string;
  pipelineDefinitionId: string;
  pipelineName: string;
  status: PipelineStatus;
  currentStepIndex: number;
  totalSteps: number;
  startedBy: string;
  triggeredBy: TriggerType;
  serviceBusMessageId?: string;
  startedAt: string;
  completedAt?: string;
  stepRuns: PipelineStepRun[];
}

export interface CreatePipelinePayload {
  name: string;
  description?: string;
  enabled: boolean;
  steps: { stepName: string; jobDefinitionId: string; outputMappingJson?: string }[];
}

// ---- Service Bus types ----------------------------------------------------

export interface ServiceBusMessage {
  messageId: string;
  action: 'TriggerJob' | 'TriggerPipeline';
  targetId: string;
  payloadJson?: string;
  sentBy: string;
  enqueuedAt: string;
  notes?: string;
}

export interface SendMessagePayload {
  action: 'TriggerJob' | 'TriggerPipeline';
  targetId: string;
  payloadJson?: string;
  notes?: string;
}
