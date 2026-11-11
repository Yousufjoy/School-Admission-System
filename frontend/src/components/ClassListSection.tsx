import type { ClassSummary } from '../types/admission';

interface ClassListSectionProps {
  classes: ClassSummary[];
  isLoading: boolean;
  applyingClassId: string | null;
  onApply: (classId: string) => void;
  onRefresh: () => void;
}

export function ClassListSection({
  classes,
  isLoading,
  applyingClassId,
  onApply,
  onRefresh,
}: ClassListSectionProps) {
  return (
    <section
      aria-labelledby="class-list-heading"
      className="rounded-2xl bg-white border border-slate-200 shadow-xs p-5"
    >
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4">
        <div>
          <h2 id="class-list-heading" className="text-lg font-bold text-slate-900">
            ক্লাস তালিকা
          </h2>
          <p className="text-xs text-slate-500 mt-0.5 flex items-center gap-1.5">
            <span className="inline-block h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
            প্রতি ৩ সেকেন্ডে স্বয়ংক্রিয়ভাবে আসন সংখ্যা আপডেট হচ্ছে
          </p>
        </div>

        <button
          type="button"
          onClick={onRefresh}
          className="self-start sm:self-auto rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
        >
          রিফ্রেশ করুন
        </button>
      </div>

      {isLoading && classes.length === 0 ? (
        <div className="py-10 text-center text-sm text-slate-500">
          ক্লাসের তালিকা লোড হচ্ছে...
        </div>
      ) : classes.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 py-10 px-4 text-center">
          <p className="text-sm font-medium text-slate-700">
            এখনো কোনো ক্লাস তৈরি করা হয়নি।
          </p>
          <p className="text-xs text-slate-500 mt-1">
            বাম পাশের ফর্ম ব্যবহার করে প্রথম ক্লাস তৈরি করুন।
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                <th className="py-3 pr-4">ক্লাসের নাম</th>
                <th className="py-3 px-3 text-center">মোট</th>
                <th className="py-3 px-3 text-center">হোল্ড</th>
                <th className="py-3 px-3 text-center">নিশ্চিত</th>
                <th className="py-3 px-3 text-center">খালি</th>
                <th className="py-3 pl-4 text-right">কার্যক্রম</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-sm">
              {classes.map((cls) => {
                const isApplying = applyingClassId === cls.id;
                const hasAvailableSeats = cls.availableSeats > 0;

                return (
                  <tr key={cls.id} className="hover:bg-slate-50/80 transition-colors">
                    <td className="py-3.5 pr-4">
                      <div className="font-semibold text-slate-900">{cls.name}</div>
                      <div className="text-xs font-mono text-slate-400">{cls.id}</div>
                    </td>
                    <td className="py-3.5 px-3 text-center font-semibold tabular-nums text-slate-700">
                      {cls.totalSeats}
                    </td>
                    <td className="py-3.5 px-3 text-center font-semibold tabular-nums text-amber-700">
                      {cls.heldSeats}
                    </td>
                    <td className="py-3.5 px-3 text-center font-semibold tabular-nums text-indigo-700">
                      {cls.confirmedSeats}
                    </td>
                    <td className="py-3.5 px-3 text-center">
                      <span
                        className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold tabular-nums ${
                          hasAvailableSeats
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-rose-100 text-rose-800'
                        }`}
                      >
                        {cls.availableSeats}
                      </span>
                    </td>
                    <td className="py-3.5 pl-4 text-right">
                      <button
                        type="button"
                        disabled={isApplying}
                        onClick={() => onApply(cls.id)}
                        className={`inline-flex items-center justify-center rounded-lg px-3.5 py-2 text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${
                          hasAvailableSeats
                            ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                            : 'bg-slate-200 text-slate-700 hover:bg-slate-300'
                        }`}
                      >
                        {isApplying ? 'আবেদন হচ্ছে...' : 'আবেদন করুন'}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

