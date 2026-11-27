// Small fetch wrapper for the Leave Tracker API (LT-003, reusable by LT-004).
// Every failure becomes one shape: { status, message, fieldErrors }.

export const NETWORK_MESSAGE =
  'Could not reach the leave tracker. Check that it is running and try again.';

export class ApiError extends Error {
  /**
   * @param {number} status HTTP status, or 0 when the server could not be reached.
   * @param {string} message Plain-words message to show.
   * @param {Record<string, string[]>} fieldErrors Messages per field (from ValidationProblem).
   */
  constructor(status, message, fieldErrors = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

/**
 * Calls the API.
 * @param {string} path Route under the site, e.g. "/api/employees".
 * @param {{ method?: string, body?: unknown, employeeId?: number|string|null }} [options]
 * @returns {Promise<any>} The parsed JSON body (or null for an empty body).
 */
export async function api(path, { method = 'GET', body, employeeId } = {}) {
  const headers = { Accept: 'application/json' };
  if (employeeId !== undefined && employeeId !== null && employeeId !== '') {
    headers['X-Employee-Id'] = String(employeeId);
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  let response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, NETWORK_MESSAGE);
  }

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (response.ok) {
    return data;
  }

  const fieldErrors = (data && typeof data.errors === 'object' && data.errors) || {};
  const firstFieldMessage = Object.values(fieldErrors).flat()[0];
  const message =
    (data && (data.detail || firstFieldMessage || data.title)) ||
    `The leave tracker could not do this (error ${response.status}). Please try again.`;
  throw new ApiError(response.status, message, fieldErrors);
}
