// Display helpers (LT-003, reusable by LT-004). Status and type are always shown as words (spec AC11).

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export const STATUS_WORDS = Object.freeze({
  pending: 'Pending',
  approved: 'Approved',
  rejected: 'Rejected',
});

export const TYPE_WORDS = Object.freeze({
  annual: 'Annual',
  sick: 'Sick',
  unpaid: 'Unpaid',
});

/**
 * "2026-12-14" → "14 Dec 2026". The parts are parsed by hand, so there is no time-zone shift (spec Q2).
 * Anything unexpected is returned unchanged.
 */
export function formatDay(isoDay) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDay ?? '');
  if (!match) {
    return isoDay ?? '';
  }
  const [, year, month, day] = match;
  const monthName = MONTHS[Number(month) - 1];
  return monthName ? `${Number(day)} ${monthName} ${year}` : isoDay;
}

/** "pending" → "Pending" (never an empty string, so status is always readable as text). */
export function statusWord(status) {
  return STATUS_WORDS[status] ?? (status ? String(status) : 'Unknown');
}

/** "annual" → "Annual". */
export function typeWord(type) {
  return TYPE_WORDS[type] ?? (type ? String(type) : '');
}

/** The number of days with its unit, e.g. "1 day" or "3 days". */
export function daysText(days) {
  return days === 1 ? '1 day' : `${days} days`;
}

/** The manager's comment, or "—" when there is none (spec Q5). */
export function commentText(comment) {
  return comment && comment.trim() ? comment : '—';
}
