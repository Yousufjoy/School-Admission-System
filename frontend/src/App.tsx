import { useCallback, useEffect, useRef, useState } from 'react';
import {
  AdmissionApiError,
  cancelHold,
  confirmHold,
  createClass,
  createHold,
  fetchClasses,
  fetchMyHolds,
} from './api/admissionApi';
import { ClassCreationForm } from './components/ClassCreationForm';
import { ClassListSection } from './components/ClassListSection';
import { HeaderBar } from './components/HeaderBar';
import { MyHoldsSection } from './components/MyHoldsSection';
import { StatusBanner } from './components/StatusBanner';
import type { ClassSummary, FeedbackMessage, StudentHoldItem } from './types/admission';
import {
  getEffectiveHoldStatus,
  isValidStudentId,
  loadStoredStudentId,
  saveStoredStudentId,
  toBengaliErrorMessage,
} from './utils/admissionUtils';

const POLL_INTERVAL_MS = 3000;
const CLOCK_TICK_MS = 500;

function extractErrorMessage(error: unknown): string {
  return error instanceof AdmissionApiError
    ? error.userMessage
    : toBengaliErrorMessage(0);
}

export function App() {
  const [studentId, setStudentId] = useState<string>(() => loadStoredStudentId());
  const [classes, setClasses] = useState<ClassSummary[]>([]);
  const [holds, setHolds] = useState<StudentHoldItem[]>([]);
  const [isLoadingClasses, setIsLoadingClasses] = useState<boolean>(true);
  const [isLoadingHolds, setIsLoadingHolds] = useState<boolean>(() =>
    isValidStudentId(loadStoredStudentId()),
  );
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);
  const [busyHoldId, setBusyHoldId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<FeedbackMessage | null>(null);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());

  const expiredSyncedIdsRef = useRef<Set<string>>(new Set());

  const loadClasses = useCallback(async (signal?: AbortSignal) => {
    try {
      const data = await fetchClasses(signal);
      setClasses(data);
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return;
      }

      setFeedback({ tone: 'error', text: extractErrorMessage(error) });
    } finally {
      setIsLoadingClasses(false);
    }
  }, []);

  const loadHolds = useCallback(
    async (activeStudentId: string, signal?: AbortSignal) => {
      if (!isValidStudentId(activeStudentId)) {
        setHolds([]);
        setIsLoadingHolds(false);
        return;
      }

      try {
        const data = await fetchMyHolds(activeStudentId, signal);
        setHolds(data);
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setFeedback({ tone: 'error', text: extractErrorMessage(error) });
      } finally {
        setIsLoadingHolds(false);
      }
    },
    [],
  );

  // Initial fetch + 3-second polling for classes and active student holds
  useEffect(() => {
    const controller = new AbortController();

    const syncOnce = () =>
      Promise.resolve().then(async () => {
        if (controller.signal.aborted) {
          return;
        }

        await loadClasses(controller.signal);
        if (isValidStudentId(studentId) && !controller.signal.aborted) {
          await loadHolds(studentId, controller.signal);
        }
      });

    void syncOnce();

    const pollTimer = window.setInterval(() => {
      void syncOnce();
    }, POLL_INTERVAL_MS);

    return () => {
      controller.abort();
      window.clearInterval(pollTimer);
    };
  }, [studentId, loadClasses, loadHolds]);

  // Real-time countdown clock tick
  useEffect(() => {
    const clockTimer = window.setInterval(() => {
      setNowMs(Date.now());
    }, CLOCK_TICK_MS);

    return () => {
      window.clearInterval(clockTimer);
    };
  }, []);

  // When a hold expires locally at 00:00, sync class seats cleanly once
  useEffect(() => {
    const newlyExpired = holds.filter(
      (hold) =>
        hold.status === 'HELD' &&
        getEffectiveHoldStatus(hold.status, hold.expiresAt, nowMs) === 'EXPIRED' &&
        !expiredSyncedIdsRef.current.has(hold.holdId),
    );

    if (newlyExpired.length === 0) {
      return;
    }

    for (const hold of newlyExpired) {
      expiredSyncedIdsRef.current.add(hold.holdId);
    }

    void loadClasses();
  }, [holds, nowMs, loadClasses]);

  function handleStudentIdChange(nextValue: string) {
    setStudentId(nextValue);
    saveStoredStudentId(nextValue);

    if (isValidStudentId(nextValue)) {
      setIsLoadingHolds(true);
    } else {
      setHolds([]);
      setIsLoadingHolds(false);
    }
  }

  async function handleCreateClass(name: string, totalSeats: number): Promise<boolean> {
    try {
      const created = await createClass({ name, totalSeats });
      setFeedback({
        tone: 'success',
        text: `ক্লাস "${created.name}" সফলভাবে তৈরি হয়েছে (${created.totalSeats}টি আসন)।`,
      });
      await loadClasses();
      return true;
    } catch (error) {
      setFeedback({ tone: 'error', text: extractErrorMessage(error) });
      return false;
    }
  }

  async function handleApplyForSeat(classId: string) {
    if (!isValidStudentId(studentId)) {
      setFeedback({
        tone: 'error',
        text: 'আবেদন করার আগে উপরে সঠিক ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।',
      });
      return;
    }

    setApplyingClassId(classId);
    try {
      await createHold(classId, studentId);
      setFeedback({
        tone: 'success',
        text: 'আসনটি সাময়িকভাবে হোল্ড করা হয়েছে। সময় শেষ হওয়ার আগে ভর্তি ফি দিন।',
      });
      await Promise.all([loadClasses(), loadHolds(studentId)]);
    } catch (error) {
      setFeedback({ tone: 'error', text: extractErrorMessage(error) });
      await loadClasses();
    } finally {
      setApplyingClassId(null);
    }
  }

  async function runHoldAction(
    holdId: string,
    action: (id: string, activeStudentId: string) => Promise<unknown>,
    successFeedback: FeedbackMessage,
  ) {
    if (!isValidStudentId(studentId)) {
      setFeedback({
        tone: 'error',
        text: 'অনুগ্রহ করে সঠিক ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।',
      });
      return;
    }

    setBusyHoldId(holdId);
    try {
      await action(holdId, studentId);
      setFeedback(successFeedback);
      await Promise.all([loadHolds(studentId), loadClasses()]);
    } catch (error) {
      setFeedback({ tone: 'error', text: extractErrorMessage(error) });
      await Promise.all([loadHolds(studentId), loadClasses()]);
    } finally {
      setBusyHoldId(null);
    }
  }

  const visibleHolds = isValidStudentId(studentId) ? holds : [];

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <HeaderBar studentId={studentId} onStudentIdChange={handleStudentIdChange} />

      <main className="max-w-6xl mx-auto px-4 sm:px-6 py-6 space-y-6">
        <StatusBanner feedback={feedback} onDismiss={() => setFeedback(null)} />

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          <div className="lg:col-span-4">
            <ClassCreationForm
              onCreateClass={handleCreateClass}
              onValidationError={(text) => setFeedback({ tone: 'error', text })}
            />
          </div>

          <div className="lg:col-span-8">
            <ClassListSection
              classes={classes}
              isLoading={isLoadingClasses}
              applyingClassId={applyingClassId}
              onApply={(classId) => void handleApplyForSeat(classId)}
              onRefresh={() => void loadClasses()}
            />
          </div>
        </div>

        <MyHoldsSection
          studentId={studentId}
          holds={visibleHolds}
          nowMs={nowMs}
          isLoading={isLoadingHolds}
          busyHoldId={busyHoldId}
          onConfirmHold={(holdId) =>
            void runHoldAction(holdId, confirmHold, {
              tone: 'success',
              text: 'ভর্তি ফি গৃহীত হয়েছে এবং আপনার আসনটি নিশ্চিত করা হয়েছে!',
            })
          }
          onCancelHold={(holdId) =>
            void runHoldAction(holdId, cancelHold, {
              tone: 'info',
              text: 'আবেদনটি বাতিল করা হয়েছে এবং আসনটি মুক্ত করা হয়েছে।',
            })
          }
        />
      </main>
    </div>
  );
}

export default App;
