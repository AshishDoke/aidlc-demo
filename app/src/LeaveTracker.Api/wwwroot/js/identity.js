// "Who are you?" picker (LT-003, reusable by LT-004).
// DEMO LIMITATION: there is no login. The picked employee id is sent as X-Employee-Id and trusted by the API.

import { api } from './api.js';

const STORAGE_KEY = 'leaveTracker.employeeId';

/** The remembered employee id, or null. Storage may be blocked (private mode), so never throw. */
export function rememberedEmployeeId() {
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

/** Remembers (or with null, forgets) the picked employee id in this browser. */
export function rememberEmployeeId(id) {
  try {
    if (id === null || id === '') {
      window.localStorage.removeItem(STORAGE_KEY);
    } else {
      window.localStorage.setItem(STORAGE_KEY, String(id));
    }
  } catch {
    // Storage blocked: the page still works, it just will not remember the choice.
  }
}

/**
 * Loads all employees, fills the picker and selects the remembered person if they still exist.
 * An unknown remembered id is forgotten silently (spec AC4).
 * @param {HTMLSelectElement} select The picker, whose first option is the "pick who you are" placeholder.
 * @returns {Promise<{ employees: any[], current: any|null }>}
 */
export async function loadPicker(select) {
  const employees = await api('/api/employees');

  while (select.options.length > 1) {
    select.remove(1);
  }
  for (const e of employees) {
    const option = document.createElement('option');
    option.value = String(e.id);
    option.textContent = e.isManager ? `${e.name} (manager)` : e.name;
    select.append(option);
  }

  const remembered = rememberedEmployeeId();
  const current = employees.find((e) => String(e.id) === remembered) ?? null;
  if (current) {
    select.value = String(current.id);
  } else {
    select.value = '';
    if (remembered !== null) {
      rememberEmployeeId(null);
    }
  }

  return { employees, current };
}

/** The employee object for the picker's current value, or null. */
export function pickedEmployee(select, employees) {
  return employees.find((e) => String(e.id) === select.value) ?? null;
}
