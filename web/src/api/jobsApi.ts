import axios from 'axios';
import type { CreateJobPayload, JobDefinition, JobRun, JobTarget, UpdateJobPayload } from '../types';

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

// ---- Job Definitions -------------------------------------------------------

export const getJobs = (target: JobTarget): Promise<JobDefinition[]> =>
  api.get<JobDefinition[]>('/jobs', { params: { target } }).then(r => r.data);

export const getJob = (id: string): Promise<JobDefinition> =>
  api.get<JobDefinition>(`/jobs/${id}`).then(r => r.data);

export const createJob = (payload: CreateJobPayload): Promise<JobDefinition> =>
  api.post<JobDefinition>('/jobs', payload).then(r => r.data);

export const updateJob = (id: string, payload: UpdateJobPayload): Promise<JobDefinition> =>
  api.put<JobDefinition>(`/jobs/${id}`, payload).then(r => r.data);

export const setJobEnabled = (id: string, enabled: boolean): Promise<{ id: string; enabled: boolean }> =>
  api.patch(`/jobs/${id}/enabled`, { enabled }).then(r => r.data);

export const deleteJob = (id: string): Promise<void> =>
  api.delete(`/jobs/${id}`).then(() => undefined);

export const triggerJob = (id: string, force = false): Promise<JobRun> =>
  api.post<JobRun>(`/jobs/${id}/run`, null, { params: { force } }).then(r => r.data);

export const getJobRuns = (jobId: string): Promise<JobRun[]> =>
  api.get<JobRun[]>(`/jobs/${jobId}/runs`).then(r => r.data);

// ---- Runs ------------------------------------------------------------------

export const getRun = (runId: string): Promise<JobRun> =>
  api.get<JobRun>(`/runs/${runId}`).then(r => r.data);

export const rerunRun = (runId: string): Promise<JobRun> =>
  api.post<JobRun>(`/runs/${runId}/rerun`).then(r => r.data);
