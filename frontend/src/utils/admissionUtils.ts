import type { HoldStatus } from '../types/admission';

export const STUDENT_ID_STORAGE_KEY = 'school_admission_student_id';

const STUDENT_ID_PATTERN = /^\d{17}$/;

export function isValidStudentId(value: string): boolean {
  return STUDENT_ID_PATTERN.test(value.trim());
}

export function loadStoredStudentId(): string {
  try {
    return window.localStorage.getItem(STUDENT_ID_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

export function saveStoredStudentId(studentId: string): void {
  try {
    if (studentId.length === 0) {
      window.localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
      return;
    }

    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, studentId);
  } catch {
    // Ignore storage quota or private-mode errors gracefully.
  }
}

export function getRemainingSeconds(expiresAtIso: string, nowMs: number): number {
  const expiresAtMs = Date.parse(expiresAtIso);
  if (Number.isNaN(expiresAtMs) || expiresAtMs <= nowMs) {
    return 0;
  }

  return Math.ceil((expiresAtMs - nowMs) / 1000);
}

export function getEffectiveHoldStatus(
  status: HoldStatus,
  expiresAtIso: string,
  nowMs: number,
): HoldStatus {
  if (status === 'HELD' && getRemainingSeconds(expiresAtIso, nowMs) <= 0) {
    return 'EXPIRED';
  }

  return status;
}

export function formatCountdown(totalSeconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(totalSeconds));
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function toBengaliErrorMessage(status: number, serverMessage = ''): string {
  const normalized = serverMessage.toLowerCase();

  if (status === 409) {
    if (normalized.includes('no available seats')) {
      return 'দুঃখিত, কোনো আসন খালি নেই।';
    }

    if (normalized.includes('already has')) {
      return 'আপনার ইতিমধ্যেই এই ক্লাসে একটি সক্রিয় বা নিশ্চিত আবেদন রয়েছে।';
    }

    if (
      normalized.includes('expired') ||
      normalized.includes('cancelled') ||
      normalized.includes('cannot be confirmed')
    ) {
      return 'দুঃখিত, এই আবেদনের সময় শেষ হয়ে গেছে অথবা বাতিল করা হয়েছে।';
    }

    return 'দুঃখিত, বর্তমানে এই অনুরোধটি সম্পন্ন করা যাচ্ছে না।';
  }

  if (status === 400) {
    if (normalized.includes('x-student-id') || normalized.includes('17-digit')) {
      return 'অনুগ্রহ করে সঠিক ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।';
    }

    if (normalized.includes('name') || normalized.includes('totalseats') || normalized.includes('validation')) {
      return 'অনুগ্রহ করে সঠিক ক্লাসের নাম এবং আসন সংখ্যা (কমপক্ষে ১) দিন।';
    }

    return 'প্রদত্ত তথ্য সঠিক নয়। অনুগ্রহ করে আবার যাচাই করুন।';
  }

  if (status === 403) {
    return 'এই আবেদনে আপনার অনুমতি নেই। সঠিক জন্ম নিবন্ধন নম্বর ব্যবহার করুন।';
  }

  if (status === 404) {
    return 'অনুরোধকৃত তথ্যটি খুঁজে পাওয়া যায়নি। তালিকা রিফ্রেশ করে আবার চেষ্টা করুন।';
  }

  if (status === 503) {
    return 'সার্ভারের ডাটাবেস এই মুহূর্তে সংযুক্ত নয়। কিছুক্ষণ পর আবার চেষ্টা করুন।';
  }

  if (status === 0) {
    return 'সার্ভারের সাথে সংযোগ স্থাপন করা যায়নি। ব্যাকএন্ড চালু আছে কিনা পরীক্ষা করুন।';
  }

  return 'একটি অপ্রত্যাশিত সমস্যা হয়েছে। অনুগ্রহ করে কিছুক্ষণ পর আবার চেষ্টা করুন।';
}

