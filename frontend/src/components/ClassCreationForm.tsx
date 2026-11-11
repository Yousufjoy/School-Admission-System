import { useState, type FormEvent } from 'react';

interface ClassCreationFormProps {
  onCreateClass: (name: string, totalSeats: number) => Promise<boolean>;
  onValidationError: (message: string) => void;
}

export function ClassCreationForm({
  onCreateClass,
  onValidationError,
}: ClassCreationFormProps) {
  const [name, setName] = useState('');
  const [totalSeatsRaw, setTotalSeatsRaw] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedName = name.trim();
    const parsedSeats = Number(totalSeatsRaw);

    if (trimmedName.length === 0) {
      onValidationError('অনুগ্রহ করে ক্লাসের নাম লিখুন।');
      return;
    }

    if (!Number.isInteger(parsedSeats) || parsedSeats < 1) {
      onValidationError('আসন সংখ্যা কমপক্ষে ১ বা তার বেশি পূর্ণসংখ্যা হতে হবে।');
      return;
    }

    setIsSubmitting(true);
    try {
      const created = await onCreateClass(trimmedName, parsedSeats);
      if (created) {
        setName('');
        setTotalSeatsRaw('');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section
      aria-labelledby="create-class-heading"
      className="rounded-2xl bg-white border border-slate-200 shadow-xs p-5"
    >
      <div className="mb-4">
        <span className="inline-block rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-semibold text-indigo-700 mb-1.5">
          অ্যাডমিন প্যানেল
        </span>
        <h2 id="create-class-heading" className="text-lg font-bold text-slate-900">
          নতুন ক্লাস তৈরি করুন
        </h2>
        <p className="text-xs text-slate-500 mt-0.5">
          ক্লাসের নাম এবং মোট আসন সংখ্যা নির্ধারণ করে নতুন ক্লাস যোগ করুন।
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4" noValidate>
        <div>
          <label
            htmlFor="class-name-input"
            className="block text-sm font-medium text-slate-700 mb-1"
          >
            ক্লাসের নাম
          </label>
          <input
            id="class-name-input"
            type="text"
            maxLength={200}
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="যেমন: প্রথম শ্রেণি - ক"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <div>
          <label
            htmlFor="total-seats-input"
            className="block text-sm font-medium text-slate-700 mb-1"
          >
            আসন সংখ্যা
          </label>
          <input
            id="total-seats-input"
            type="number"
            min={1}
            step={1}
            value={totalSeatsRaw}
            onChange={(event) => setTotalSeatsRaw(event.target.value)}
            placeholder="যেমন: ৩০"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm tabular-nums focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
        >
          {isSubmitting ? 'তৈরি হচ্ছে...' : 'ক্লাস তৈরি করুন'}
        </button>
      </form>
    </section>
  );
}

