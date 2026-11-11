import type { HoldStatus, StudentHoldItem } from '../types/admission';
import {
  formatCountdown,
  getEffectiveHoldStatus,
  getRemainingSeconds,
  isValidStudentId,
} from '../utils/admissionUtils';

interface MyHoldsSectionProps {
  studentId: string;
  holds: StudentHoldItem[];
  nowMs: number;
  isLoading: boolean;
  busyHoldId: string | null;
  onConfirmHold: (holdId: string) => void;
  onCancelHold: (holdId: string) => void;
}

const statusBadgeStyles: Record<HoldStatus, string> = {
  HELD: 'bg-amber-100 text-amber-900 border-amber-200',
  CONFIRMED: 'bg-emerald-100 text-emerald-900 border-emerald-200',
  EXPIRED: 'bg-rose-100 text-rose-900 border-rose-200',
  CANCELLED: 'bg-slate-100 text-slate-700 border-slate-200',
};

const statusBengaliLabel: Record<HoldStatus, string> = {
  HELD: 'সাময়িক হোল্ড',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিলকৃত',
};

export function MyHoldsSection({
  studentId,
  holds,
  nowMs,
  isLoading,
  busyHoldId,
  onConfirmHold,
  onCancelHold,
}: MyHoldsSectionProps) {
  const hasValidStudentId = isValidStudentId(studentId);

  return (
    <section
      aria-labelledby="my-holds-heading"
      className="rounded-2xl bg-white border border-slate-200 shadow-xs p-5"
    >
      <div className="mb-4">
        <h2 id="my-holds-heading" className="text-lg font-bold text-slate-900">
          আমার আবেদন
        </h2>
        <p className="text-xs text-slate-500 mt-0.5">
          আপনার চলমান ও পূর্ববর্তী সকল আসন হোল্ডের অবস্থা এবং নির্ধারিত সময়সীমা।
        </p>
      </div>

      {!hasValidStudentId ? (
        <div className="rounded-xl border border-dashed border-amber-300 bg-amber-50/70 py-8 px-4 text-center">
          <p className="text-sm font-medium text-amber-900">
            আপনার আবেদনসমূহ দেখতে উপরে ১৭ ডিজিটের জন্ম নিবন্ধন নম্বর দিন।
          </p>
        </div>
      ) : isLoading && holds.length === 0 ? (
        <div className="py-8 text-center text-sm text-slate-500">
          আপনার আবেদন তালিকা লোড হচ্ছে...
        </div>
      ) : holds.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 py-8 px-4 text-center">
          <p className="text-sm font-medium text-slate-700">
            আপনার কোনো আবেদন এখনো পাওয়া যায়নি।
          </p>
          <p className="text-xs text-slate-500 mt-1">
            উপরের ক্লাস তালিকা থেকে পছন্দের ক্লাসে &ldquo;আবেদন করুন&rdquo; বোতামে ক্লিক করুন।
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {holds.map((hold) => {
            const effectiveStatus = getEffectiveHoldStatus(hold.status, hold.expiresAt, nowMs);
            const remainingSeconds =
              hold.status === 'HELD' ? getRemainingSeconds(hold.expiresAt, nowMs) : 0;
            const isBusy = busyHoldId === hold.holdId;
            const canConfirm = effectiveStatus === 'HELD' && !isBusy;
            const canCancel =
              (effectiveStatus === 'HELD' || effectiveStatus === 'CONFIRMED') && !isBusy;

            return (
              <article
                key={hold.holdId}
                className="rounded-xl border border-slate-200 bg-slate-50/50 p-4 flex flex-col justify-between gap-4"
              >
                <div className="space-y-2">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h3 className="font-bold text-slate-900">
                        {hold.className || hold.classId}
                      </h3>
                      <p className="text-xs font-mono text-slate-500">
                        আবেদন আইডি: {hold.holdId}
                      </p>
                    </div>

                    <div className="text-right">
                      <span
                        className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${statusBadgeStyles[effectiveStatus]}`}
                      >
                        {effectiveStatus}
                      </span>
                      <div className="text-[11px] text-slate-500 mt-0.5">
                        {statusBengaliLabel[effectiveStatus]}
                      </div>
                    </div>
                  </div>

                  {hold.status === 'HELD' && (
                    <div
                      className={`rounded-lg px-3 py-2 flex items-center justify-between border ${
                        effectiveStatus === 'HELD'
                          ? 'bg-amber-50 border-amber-200 text-amber-900'
                          : 'bg-rose-50 border-rose-200 text-rose-900'
                      }`}
                    >
                      <span className="text-xs font-medium">
                        {effectiveStatus === 'HELD' ? 'অবশিষ্ট সময়' : 'সময় শেষ হয়েছে'}
                      </span>
                      <span
                        data-testid={`countdown-${hold.holdId}`}
                        className="font-mono text-base font-bold tabular-nums"
                      >
                        {formatCountdown(remainingSeconds)}
                      </span>
                    </div>
                  )}
                </div>

                <div className="flex items-center gap-2.5 pt-1">
                  <button
                    type="button"
                    disabled={!canConfirm}
                    onClick={() => onConfirmHold(hold.holdId)}
                    className="flex-1 rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white hover:bg-indigo-700 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    ভর্তি ফি দিন
                  </button>

                  <button
                    type="button"
                    disabled={!canCancel}
                    onClick={() => onCancelHold(hold.holdId)}
                    className="rounded-lg border border-rose-300 bg-white px-3 py-2 text-xs font-semibold text-rose-700 hover:bg-rose-50 disabled:border-slate-200 disabled:text-slate-400 disabled:bg-slate-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    বাতিল
                  </button>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

