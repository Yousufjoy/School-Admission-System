export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface ClassSummary {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface CreateClassInput {
  name: string;
  totalSeats: number;
}

export interface CreateClassResult {
  id: string;
  name: string;
  totalSeats: number;
}

export interface CreateHoldResult {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface ConfirmHoldResult {
  holdId: string;
  status: HoldStatus;
}

export interface StudentHoldItem {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface ApiErrorPayload {
  error?: string;
  statusCode?: number;
  errors?: Record<string, string[]>;
}

export type FeedbackTone = 'success' | 'error' | 'info';

export interface FeedbackMessage {
  tone: FeedbackTone;
  text: string;
}

