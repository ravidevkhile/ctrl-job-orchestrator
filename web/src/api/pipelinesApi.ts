import axios from 'axios';
import type {
  CreatePipelinePayload,
  PipelineDefinition,
  PipelineRun,
  SendMessagePayload,
  ServiceBusMessage,
} from '../types';

const api = axios.create({ baseURL: '/api', headers: { 'Content-Type': 'application/json' } });

// ---- Pipelines -------------------------------------------------------------

export const getPipelines = (): Promise<PipelineDefinition[]> =>
  api.get<PipelineDefinition[]>('/pipelines').then(r => r.data);

export const getPipeline = (id: string): Promise<PipelineDefinition> =>
  api.get<PipelineDefinition>(`/pipelines/${id}`).then(r => r.data);

export const createPipeline = (p: CreatePipelinePayload): Promise<PipelineDefinition> =>
  api.post<PipelineDefinition>('/pipelines', p).then(r => r.data);

export const updatePipeline = (id: string, p: Partial<CreatePipelinePayload>): Promise<PipelineDefinition> =>
  api.put<PipelineDefinition>(`/pipelines/${id}`, p).then(r => r.data);

export const setPipelineEnabled = (id: string, enabled: boolean) =>
  api.patch(`/pipelines/${id}/enabled`, { enabled }).then(r => r.data);

export const deletePipeline = (id: string): Promise<void> =>
  api.delete(`/pipelines/${id}`).then(() => undefined);

export const triggerPipeline = (id: string): Promise<PipelineRun> =>
  api.post<PipelineRun>(`/pipelines/${id}/run`).then(r => r.data);

export const getPipelineRuns = (id: string): Promise<PipelineRun[]> =>
  api.get<PipelineRun[]>(`/pipelines/${id}/runs`).then(r => r.data);

export const getPipelineRun = (runId: string): Promise<PipelineRun> =>
  api.get<PipelineRun>(`/pipelines/runs/${runId}`).then(r => r.data);

// ---- Service Bus -----------------------------------------------------------

export const sendServiceBusMessage = (payload: SendMessagePayload): Promise<ServiceBusMessage> =>
  api.post<ServiceBusMessage>('/servicebus/messages', payload).then(r => r.data);

export const getRecentMessages = (): Promise<ServiceBusMessage[]> =>
  api.get<ServiceBusMessage[]>('/servicebus/messages').then(r => r.data);
