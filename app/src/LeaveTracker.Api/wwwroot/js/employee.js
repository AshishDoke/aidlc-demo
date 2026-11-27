// Employee page (LT-003): pick who you are, ask for leave, see "My requests".
// All business rules are checked by the API; this page only shows what the API says.
// Dynamic text is always set with textContent, so reasons and comments can never inject markup.

import { api, ApiError, NETWORK_MESSAGE } from './api.js';
import { loadPicker, pickedEmployee, rememberEmployeeId } from './identity.js';
import { commentText, daysText, formatDay, statusWord, typeWord } from './format.js';

/** API field name → form control id. */
const FIELDS = {
  firstDay: 'first-day',
  lastDay: 'last-day',
  type: 'type',
  reason: 'reason',
};

const el = (id) => document.getElementById(id);

const who = el('who');
const managerLink = el('manager-link');
const loadError = el('load-error');
const pickFirst = el('pick-first');
const form = el('leave-form');
const fields = el('leave-fields');
const sendButton = el('send-button');
const formErrors = el('form-errors');
const formStatus = el('form-status');
const mineEmpty = el('mine-empty');
const mineTable = el('mine-table');
const mineRows = el('mine-rows');

let employees = [];
let current = null;
let listToken = 0; // ignores out-of-date list responses after a quick switch of person

function show(node, visible) {
  node.hidden = !visible;
}

function setText(node, text) {
  node.textContent = text;
  show(node, Boolean(text));
}

function clearMessages() {
  setText(formErrors, '');
  setText(formStatus, '');
  for (const id of Object.values(FIELDS)) {
    el(id).removeAttribute('aria-invalid');
    setText(el(`${id}-error`), '');
  }
}

function applyPerson(person) {
  current = person;
  fields.disabled = !person;
  show(pickFirst, !person);
  show(managerLink, Boolean(person && person.isManager));
  clearMessages();
  loadMyRequests();
}

function renderRows(requests) {
  mineRows.replaceChildren();
  for (const r of requests) {
    const row = document.createElement('tr');

    const cell = (text, className) => {
      const td = document.createElement('td');
      td.textContent = text;
      if (className) {
        td.className = className;
      }
      row.append(td);
      return td;
    };

    cell(formatDay(r.firstDay));
    cell(formatDay(r.lastDay));
    cell(typeWord(r.type));
    cell(String(r.daysCovered), 'num');

    const statusCell = cell('');
    const badge = document.createElement('span');
    badge.className = `status status-${r.status}`;
    badge.textContent = statusWord(r.status);
    statusCell.append(badge);

    cell(commentText(r.decisionComment));
    mineRows.append(row);
  }
}

async function loadMyRequests() {
  const token = ++listToken;

  if (!current) {
    mineRows.replaceChildren();
    show(mineTable, false);
    setText(mineEmpty, 'Pick who you are to see your requests.');
    return;
  }

  try {
    const requests = await api('/api/me/leave-requests', { employeeId: current.id });
    if (token !== listToken) {
      return;
    }
    renderRows(requests);
    show(mineTable, requests.length > 0);
    setText(mineEmpty, requests.length > 0 ? '' : 'You have no leave requests yet.');
  } catch (error) {
    if (token !== listToken) {
      return;
    }
    show(mineTable, false);
    setText(mineEmpty, `Your requests could not be loaded. ${error instanceof ApiError ? error.message : NETWORK_MESSAGE}`);
  }
}

function showErrors(error) {
  const fieldErrors = error instanceof ApiError ? error.fieldErrors : {};
  let firstInvalid = null;
  const otherMessages = [];

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const id = FIELDS[field];
    const text = [].concat(messages).join(' ');
    if (id) {
      const control = el(id);
      control.setAttribute('aria-invalid', 'true');
      setText(el(`${id}-error`), text);
      firstInvalid ??= control;
    } else {
      otherMessages.push(text);
    }
  }

  const summary = Object.keys(fieldErrors).length > 0
    ? ['The request was not sent. Please correct the fields marked with an error.', ...otherMessages].join(' ')
    : `The request was not sent. ${error instanceof ApiError ? error.message : NETWORK_MESSAGE}`;
  setText(formErrors, summary);

  (firstInvalid ?? formErrors).focus();
}

async function onSubmit(event) {
  event.preventDefault();
  if (!current || sendButton.disabled) {
    return;
  }

  clearMessages();
  sendButton.disabled = true;
  sendButton.textContent = 'Sending…';

  const body = {
    firstDay: el('first-day').value || null,
    lastDay: el('last-day').value || null,
    type: el('type').value || null,
    reason: el('reason').value.trim() || null,
  };

  try {
    const created = await api('/api/leave-requests', { method: 'POST', body, employeeId: current.id });
    form.reset();
    setText(
      formStatus,
      `Request sent: ${formatDay(created.firstDay)} – ${formatDay(created.lastDay)}, ${typeWord(created.type).toLowerCase()}, ` +
        `${daysText(created.daysCovered)}. It is ${statusWord(created.status).toLowerCase()} until your manager decides.`,
    );
    await loadMyRequests();
    el('first-day').focus();
  } catch (error) {
    showErrors(error);
  } finally {
    sendButton.disabled = false;
    sendButton.textContent = 'Send request';
  }
}

function onPick() {
  const person = pickedEmployee(who, employees);
  rememberEmployeeId(person ? person.id : null);
  applyPerson(person);
}

async function start() {
  who.addEventListener('change', onPick);
  form.addEventListener('submit', onSubmit);

  try {
    const loaded = await loadPicker(who);
    employees = loaded.employees;
    setText(loadError, '');
    applyPerson(loaded.current);
  } catch (error) {
    setText(loadError, `The employee list could not be loaded. ${error instanceof ApiError ? error.message : NETWORK_MESSAGE}`);
    applyPerson(null);
  }
}

start();
