import { isValidStudentId } from '../utils/admissionUtils';

interface HeaderBarProps {
  studentId: string;
  onStudentIdChange: (value: string) => void;
}

export function HeaderBar({ studentId, onStudentIdChange }: HeaderBarProps) {
  const valid = isValidStudentId(studentId);
  const trimmedLength = studentId.trim().length;

  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-20 shadow-xs">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 py-4 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-600 text-white font-bold text-sm">
              ভর্তি
            </span>
            <h1 className="text-xl font-bold text-slate-900">
              স্কুল ভর্তি ব্যবস্থা
            </h1>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            রিয়েল-টাইম আসন সংরক্ষণ ও ভর্তি ফি নিশ্চিতকরণ পোর্টাল
          </p>
        </div>

        <div className="w-full md:w-[400px]">
          <div className="flex items-center justify-between mb-1">
            <label
              htmlFor="student-id-input"
              className="block text-sm font-semibold text-slate-700"
            >
              জন্ম নিবন্ধন নম্বর (১৭ ডিজিট)
            </label>
            <span
              className={`text-xs font-medium tabular-nums ${
                valid
                  ? 'text-emerald-700'
                  : trimmedLength > 0
                    ? 'text-amber-700'
                    : 'text-slate-500'
              }`}
            >
              {valid ? '১৭/১৭ সঠিক' : `${trimmedLength}/17 ডিজিট`}
            </span>
          </div>

          <div className="flex gap-2">
            <input
              id="student-id-input"
              type="text"
              inputMode="numeric"
              maxLength={17}
              value={studentId}
              onChange={(event) => onStudentIdChange(event.target.value)}
              placeholder="যেমন: 20081234567890123"
              className={`w-full rounded-lg border px-3 py-2 text-sm font-mono tracking-wide focus:outline-none focus:ring-2 transition-colors ${
                valid
                  ? 'border-emerald-400 bg-emerald-50/40 focus:ring-emerald-500'
                  : trimmedLength > 0
                    ? 'border-amber-400 bg-amber-50/40 focus:ring-amber-500'
                    : 'border-slate-300 bg-white focus:ring-indigo-500'
              }`}
            />
            {studentId.length > 0 && (
              <button
                type="button"
                onClick={() => onStudentIdChange('')}
                className="shrink-0 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-100 transition-colors cursor-pointer"
              >
                মুছুন
              </button>
            )}
          </div>

          {!valid && trimmedLength > 0 ? (
            <p className="mt-1 text-xs text-amber-700">
              সঠিক ১৭ ডিজিটের সংখ্যা দিন (অক্ষর বা ফাঁকা স্থান গ্রহণযোগ্য নয়)।
            </p>
          ) : (
            <p className="mt-1 text-xs text-slate-500">
              ব্রাউজারে স্বয়ংক্রিয়ভাবে সংরক্ষিত থাকে।
            </p>
          )}
        </div>
      </div>
    </header>
  );
}

