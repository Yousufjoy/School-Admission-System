import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { STUDENT_ID_STORAGE_KEY } from './utils/admissionUtils';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('School Admission System Frontend', () => {
  const validStudentId = '20081234567890123';

  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('persists student ID in localStorage and attaches X-Student-Id header to API calls', async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, validStudentId);

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const headers = init?.headers as Record<string, string> | undefined;

      if (url.endsWith('/classes') && (!init?.method || init.method === 'GET')) {
        return jsonResponse([
          {
            id: 'c_11223344',
            name: 'প্রথম শ্রেণি',
            totalSeats: 10,
            heldSeats: 1,
            confirmedSeats: 2,
            availableSeats: 7,
          },
        ]);
      }

      if (url.endsWith('/me/holds')) {
        expect(headers?.['X-Student-Id']).toBe(validStudentId);
        return jsonResponse([]);
      }

      if (url.endsWith('/classes/c_11223344/holds') && init?.method === 'POST') {
        expect(headers?.['X-Student-Id']).toBe(validStudentId);
        return jsonResponse(
          {
            holdId: 'h_aabbccdd',
            classId: 'c_11223344',
            studentId: validStudentId,
            status: 'HELD',
            expiresAt: new Date(Date.now() + 60_000).toISOString(),
          },
          201,
        );
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const studentInput = screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/i) as HTMLInputElement;
    expect(studentInput.value).toBe(validStudentId);

    expect(await screen.findByText('প্রথম শ্রেণি')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'আবেদন করুন' }));

    await waitFor(() => {
      expect(
        fetchMock.mock.calls.some(
          ([url, init]) =>
            String(url).endsWith('/classes/c_11223344/holds') && init?.method === 'POST',
        ),
      ).toBe(true);
    });

    const newStudentId = '20099999999999999';
    fireEvent.change(studentInput, { target: { value: newStudentId } });
    expect(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBe(newStudentId);
  });

  it('creates a class, shows Bengali full-seat error on 409, and polls classes every 3 seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, validStudentId);

    let classGetCount = 0;

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.endsWith('/classes') && (!init?.method || init.method === 'GET')) {
        classGetCount += 1;
        return jsonResponse(
          classGetCount === 1
            ? []
            : [
                {
                  id: 'c_created1',
                  name: 'দ্বিতীয় শ্রেণি',
                  totalSeats: 1,
                  heldSeats: 1,
                  confirmedSeats: 0,
                  availableSeats: 0,
                },
              ],
        );
      }

      if (url.endsWith('/classes') && init?.method === 'POST') {
        return jsonResponse(
          { id: 'c_created1', name: 'দ্বিতীয় শ্রেণি', totalSeats: 1 },
          201,
        );
      }

      if (url.endsWith('/classes/c_created1/holds') && init?.method === 'POST') {
        return jsonResponse(
          { error: 'Class has no available seats remaining.', statusCode: 409 },
          409,
        );
      }

      if (url.endsWith('/me/holds')) {
        return jsonResponse([]);
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    await waitFor(() => expect(classGetCount).toBe(1));

    fireEvent.change(screen.getByLabelText('ক্লাসের নাম'), {
      target: { value: 'দ্বিতীয় শ্রেণি' },
    });
    fireEvent.change(screen.getByLabelText('আসন সংখ্যা'), {
      target: { value: '1' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'ক্লাস তৈরি করুন' }));

    expect(await screen.findByText('দ্বিতীয় শ্রেণি')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'আবেদন করুন' }));
    expect(
      await screen.findByText('দুঃখিত, কোনো আসন খালি নেই।'),
    ).toBeInTheDocument();

    const callsBeforePoll = classGetCount;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(3100);
    });
    expect(classGetCount).toBeGreaterThan(callsBeforePoll);
  });

  it('counts down MM:SS in real time and switches HELD to EXPIRED at 00:00 while disabling Pay Fee', async () => {
    vi.useFakeTimers();
    const baseNow = new Date('2026-11-11T10:00:00.000Z');
    vi.setSystemTime(baseNow);
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, validStudentId);

    const expiresAt = new Date(baseNow.getTime() + 3000).toISOString();

    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);

      if (url.endsWith('/classes')) {
        return jsonResponse([]);
      }

      if (url.endsWith('/me/holds')) {
        return jsonResponse([
          {
            holdId: 'h_timer123',
            classId: 'c_11223344',
            className: 'তৃতীয় শ্রেণি',
            status: 'HELD',
            expiresAt,
          },
        ]);
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    // Flush initial async fetch promises while fake timers are active
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(screen.getByTestId('countdown-h_timer123')).toHaveTextContent('00:03');
    expect(screen.getByText('HELD')).toBeInTheDocument();

    const payButton = screen.getByRole('button', { name: 'ভর্তি ফি দিন' });
    expect(payButton).toBeEnabled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3100);
    });

    expect(screen.getByTestId('countdown-h_timer123')).toHaveTextContent('00:00');
    expect(screen.getByText('EXPIRED')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
  });

  it('calls confirm and cancel endpoints for active holds', async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, validStudentId);

    let currentStatus: 'HELD' | 'CONFIRMED' | 'CANCELLED' = 'HELD';
    const expiresAt = new Date(Date.now() + 120_000).toISOString();

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.endsWith('/classes')) {
        return jsonResponse([]);
      }

      if (url.endsWith('/me/holds')) {
        return jsonResponse([
          {
            holdId: 'h_action01',
            classId: 'c_11223344',
            className: 'চতুর্থ শ্রেণি',
            status: currentStatus,
            expiresAt,
          },
        ]);
      }

      if (url.endsWith('/holds/h_action01/confirm') && init?.method === 'POST') {
        currentStatus = 'CONFIRMED';
        return jsonResponse({ holdId: 'h_action01', status: 'CONFIRMED' });
      }

      if (url.endsWith('/holds/h_action01') && init?.method === 'DELETE') {
        currentStatus = 'CANCELLED';
        return new Response(null, { status: 204 });
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    expect(await screen.findByText('চতুর্থ শ্রেণি')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'ভর্তি ফি দিন' }));
    expect(await screen.findByText('CONFIRMED')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'বাতিল' }));
    expect(await screen.findByText('CANCELLED')).toBeInTheDocument();
  });
});

