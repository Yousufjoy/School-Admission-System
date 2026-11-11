import type {
  ApiErrorPayload,
  ClassSummary,
  ConfirmHoldResult,
  CreateClassInput,
  CreateClassResult,
  CreateHoldResult,
  StudentHoldItem,
} from '../types/admission';
import { toBengaliErrorMessage } from '../utils/admissionUtils';

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;

export const API_BASE_URL =
  rawBaseUrl && rawBaseUrl.trim().length > 0
    ? rawBaseUrl.trim().replace(/\/+$/, '')
    : 'http://localhost:5159';

export class AdmissionApiError extends Error {
  public readonly status: number;
  public readonly userMessage: string;

  constructor(status: number, userMessage: string, rawMessage?: string) {
    super(rawMessage ?? userMessage);
    this.name = 'AdmissionApiError';
    this.status = status;
    this.userMessage = userMessage;
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  studentId?: string;
  signal?: AbortSignal;
}

async function parseErrorPayload(response: Response): Promise<string> {
  try {
    const text = await response.text();
    if (!text) {
      return '';
    }

    const parsed = JSON.parse(text) as ApiErrorPayload;
    if (typeof parsed.error === 'string' && parsed.error.length > 0) {
      return parsed.error;
    }

    if (parsed.errors) {
      const firstKey = Object.keys(parsed.errors)[0];
      if (firstKey && parsed.errors[firstKey]?.length) {
        return `${firstKey}: ${parsed.errors[firstKey][0]}`;
      }
    }

    return text;
  } catch {
    return '';
  }
}

async function requestJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
  };

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (options.studentId && options.studentId.trim().length > 0) {
    headers['X-Student-Id'] = options.studentId.trim();
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: options.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw new AdmissionApiError(0, toBengaliErrorMessage(0));
  }

  if (!response.ok) {
    const rawMessage = await parseErrorPayload(response);
    throw new AdmissionApiError(
      response.status,
      toBengaliErrorMessage(response.status, rawMessage),
      rawMessage,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  if (!text) {
    return undefined as T;
  }

  return JSON.parse(text) as T;
}

export function fetchClasses(signal?: AbortSignal): Promise<ClassSummary[]> {
  return requestJson<ClassSummary[]>('/classes', { signal });
}

export function createClass(input: CreateClassInput): Promise<CreateClassResult> {
  return requestJson<CreateClassResult>('/classes', {
    method: 'POST',
    body: input,
  });
}

export function createHold(classId: string, studentId: string): Promise<CreateHoldResult> {
  return requestJson<CreateHoldResult>(`/classes/${encodeURIComponent(classId)}/holds`, {
    method: 'POST',
    studentId,
  });
}

export function fetchMyHolds(studentId: string, signal?: AbortSignal): Promise<StudentHoldItem[]> {
  return requestJson<StudentHoldItem[]>('/me/holds', {
    studentId,
    signal,
  });
}

export function confirmHold(holdId: string, studentId: string): Promise<ConfirmHoldResult> {
  return requestJson<ConfirmHoldResult>(`/holds/${encodeURIComponent(holdId)}/confirm`, {
    method: 'POST',
    studentId,
  });
}

export function cancelHold(holdId: string, studentId: string): Promise<void> {
  return requestJson<void>(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'DELETE',
    studentId,
  });
}

