import type { FeedbackMessage } from '../types/admission';

interface StatusBannerProps {
  feedback: FeedbackMessage | null;
  onDismiss: () => void;
}

const toneClasses: Record<FeedbackMessage['tone'], string> = {
  success: 'bg-emerald-50 border-emerald-200 text-emerald-900',
  error: 'bg-rose-50 border-rose-200 text-rose-900',
  info: 'bg-sky-50 border-sky-200 text-sky-900',
};

export function StatusBanner({ feedback, onDismiss }: StatusBannerProps) {
  if (!feedback) {
    return null;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className={`rounded-xl border px-4 py-3 flex items-center justify-between gap-4 shadow-xs ${toneClasses[feedback.tone]}`}
    >
      <p className="text-sm font-medium">{feedback.text}</p>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="বার্তা বন্ধ করুন"
        className="shrink-0 rounded-md px-2 py-1 text-xs font-semibold opacity-75 hover:opacity-100 cursor-pointer"
      >
        বন্ধ করুন
      </button>
    </div>
  );
}

