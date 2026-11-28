// Manager page (LT-004): pick who you are, see your team's pending requests, approve or reject them.
// All business rules are checked by the API; this page only shows what the API says.
// Dynamic text is always set with textContent, so names and reasons can never inject markup.

import { api, ApiError, NETWORK_MESSAGE } from './api.js';
import { loadPicker, pickedEmployee, rememberEmployeeId } from './identity.js';
import { commentText, daysText, formatDay, typeWord } from './format.js';

const el = (id) => document.getElementById(id);

const who = el('who');
const loadError = el('load-error');
const pickFirst = el('pick-first');
const notManager = el('not-manager');
const notManagerName = el('not-manager-name');
const teamSection = el('team-section');
const waitingCount = el('waiting-count');
const pageStatus = el('page-status');
const pageError = el('page-error');
const teamTable = el('team-table');
const teamRows = el('team-rows');
const decisionTemplate = el('decision-template');

const COLUMNS = 7;

let employees = [];
let current = null;
let listToken = 0; // ignores out-of-date list responses after a quick switch of person
let openPanel = null; // the one open decision panel: { request, row, panelRow, opener, approve, textarea, error, confirm }
let sending = false; // guards against sending the same decision twice (AC10)

function show(node, visible) {
  node.hidden = !visible;
}

function setText(node, text) {
  node.textContent = text;
  show(node, Boolean(text));
}

function messageOf(error) {
  return error instanceof ApiError ? error.message : NETWORK_MESSAGE;
}

function dates(r) {
  return `${formatDay(r.firstDay)} – ${formatDay(r.lastDay)}`;
}

function requestsText(n) {
  return n === 1 ? '1 request' : `${n} requests`;
}

function countText(n) {
  return n === 0 ? 'Nothing is waiting for your decision.' : `${requestsText(n)} waiting for your decision`;
}

/** Updates the count heading and table visibility from the rows on screen; returns the count (spec Q5). */
function updateCount() {
  const n = teamRows.querySelectorAll('tr.request-row').length;
  waitingCount.textContent = countText(n);
  show(teamTable, n > 0);
  return n;
}

function clearMessages() {
  setText(pageStatus, '');
  setText(pageError, '');
}

function closePanel(restoreFocus) {
  if (!openPanel) {
    return;
  }
  const { panelRow, opener } = openPanel;
  panelRow.remove();
  openPanel = null;
  sending = false;
  if (restoreFocus && opener.isConnected) {
    opener.focus();
  }
}

function applyPerson(person) {
  current = person;
  closePanel(false);
  clearMessages();

  const isManager = Boolean(person && person.isManager);
  show(pickFirst, !person);
  notManagerName.textContent = person ? person.name : '';
  show(notManager, Boolean(person) && !isManager);
  show(teamSection, isManager);

  if (isManager) {
    loadTeam();
  } else {
    ++listToken;
    teamRows.replaceChildren();
    show(teamTable, false);
  }
}

function showNotManager() {
  show(teamSection, false);
  notManagerName.textContent = current ? current.name : '';
  show(notManager, true);
}

async function loadTeam() {
  const token = ++listToken;
  closePanel(false);
  teamRows.replaceChildren();
  show(teamTable, false);
  waitingCount.textContent = 'Loading requests…';

  try {
    const requests = await api('/api/team/leave-requests/pending', { employeeId: current.id });
    if (token !== listToken) {
      return;
    }
    renderRows(requests);
    updateCount();
  } catch (error) {
    if (token !== listToken) {
      return;
    }
    if (error instanceof ApiError && error.status === 403) {
      showNotManager(); // the person is no longer a manager
      return;
    }
    waitingCount.textContent = 'Requests could not be loaded.';
    setText(pageError, `Your team's requests could not be loaded. ${messageOf(error)}`);
  }
}

function renderRows(requests) {
  teamRows.replaceChildren();
  for (const r of requests) {
    const row = document.createElement('tr');
    row.className = 'request-row';

    const cell = (text, className) => {
      const td = document.createElement('td');
      td.textContent = text;
      if (className) {
        td.className = className;
      }
      row.append(td);
      return td;
    };

    cell(r.employeeName);
    cell(formatDay(r.firstDay));
    cell(formatDay(r.lastDay));
    cell(typeWord(r.type));
    cell(String(r.daysCovered), 'num');
    cell(commentText(r.reason));

    const buttons = document.createElement('div');
    buttons.className = 'button-row';
    const approve = decideButton('Approve', 'decide-approve', r, row, true);
    const reject = decideButton('Reject', 'button-secondary decide-reject', r, row, false);
    buttons.append(approve, reject);
    cell('').append(buttons);

    teamRows.append(row);
  }
}

function decideButton(word, className, request, row, approve) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = className;
  button.textContent = word;
  button.setAttribute('aria-label', `${word} ${request.employeeName}, ${dates(request)}`);
  button.addEventListener('click', () => openDecision(request, row, approve, button));
  return button;
}

function openDecision(request, row, approve, opener) {
  closePanel(false);

  const fragment = decisionTemplate.content.cloneNode(true);
  const form = fragment.querySelector('form');
  const title = form.querySelector('.decision-title');
  const label = form.querySelector('label');
  const textarea = form.querySelector('textarea');
  const error = form.querySelector('.field-error');
  const confirm = form.querySelector('.decision-confirm');
  const cancel = form.querySelector('.decision-cancel');

  title.textContent =
    `${approve ? 'Approve' : 'Reject'} ${request.employeeName}'s ${typeWord(request.type).toLowerCase()} leave, ` +
    `${dates(request)} (${daysText(request.daysCovered)})`;

  if (!approve) {
    const required = document.createElement('span');
    required.className = 'optional';
    required.textContent = '(required)';
    label.replaceChildren('Reason for rejecting ', required);
    textarea.setAttribute('aria-required', 'true');
    confirm.textContent = 'Confirm rejection';
  }

  const panelRow = document.createElement('tr');
  panelRow.className = 'decision-row';
  const td = document.createElement('td');
  td.colSpan = COLUMNS;
  td.append(form);
  panelRow.append(td);
  row.after(panelRow);

  openPanel = { request, row, panelRow, opener, approve, textarea, error, confirm };

  form.addEventListener('submit', onConfirm);
  cancel.addEventListener('click', () => closePanel(true));
  form.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closePanel(true);
    }
  });

  textarea.focus();
}

function showPanelError(panel, text, invalid) {
  setText(panel.error, text);
  if (invalid) {
    panel.textarea.setAttribute('aria-invalid', 'true');
  } else {
    panel.textarea.removeAttribute('aria-invalid');
  }
  // The text box is linked to the error slot (aria-describedby), so the message is read when it gets focus.
  panel.textarea.focus();
}

/** The Approve button of the row after (or else before) the given row, if any. */
function nextFocusTarget(row) {
  const rows = [...teamRows.querySelectorAll('tr.request-row')];
  const index = rows.indexOf(row);
  const neighbour = rows[index + 1] ?? rows[index - 1];
  return neighbour ? neighbour.querySelector('.decide-approve') : null;
}

async function onConfirm(event) {
  event.preventDefault();
  const panel = openPanel;
  if (!panel || sending || !current) {
    return;
  }

  sending = true;
  panel.confirm.disabled = true;
  panel.confirm.textContent = 'Sending…';
  setText(panel.error, '');
  panel.textarea.removeAttribute('aria-invalid');

  const text = panel.textarea.value.trim() || null;
  const action = panel.approve ? 'approve' : 'reject';
  const body = panel.approve ? { comment: text } : { reason: text };

  try {
    const decided = await api(`/api/leave-requests/${panel.request.id}/${action}`, {
      method: 'POST',
      body,
      employeeId: current.id,
    });
    if (openPanel !== panel) {
      return; // the person was switched while sending
    }

    const focusTarget = nextFocusTarget(panel.row);
    closePanel(false);
    panel.row.remove();
    const left = updateCount();

    setText(pageError, '');
    setText(
      pageStatus,
      `${decided.status === 'approved' ? 'Approved' : 'Rejected'}: ${decided.employeeName}, ` +
        `${typeWord(decided.type).toLowerCase()}, ${dates(decided)}. ${requestsText(left)} left.`,
    );
    (focusTarget ?? waitingCount).focus();
  } catch (error) {
    if (openPanel !== panel) {
      return;
    }

    const fieldErrors = error instanceof ApiError ? error.fieldErrors : {};
    const fieldMessage = fieldErrors.reason ?? fieldErrors.comment;

    if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
      // Already decided, not your team, or gone: say why and bring the list up to date (AC9).
      closePanel(false);
      setText(pageStatus, '');
      setText(pageError, error.message);
      pageError.focus();
      await loadTeam();
      return;
    }

    if (fieldMessage) {
      showPanelError(panel, [].concat(fieldMessage).join(' '), true);
    } else {
      showPanelError(panel, `Nothing was decided. ${messageOf(error)}`, false);
    }
  } finally {
    if (openPanel === panel) {
      sending = false;
      panel.confirm.disabled = false;
      panel.confirm.textContent = panel.approve ? 'Confirm approval' : 'Confirm rejection';
    }
  }
}

function onPick() {
  const person = pickedEmployee(who, employees);
  rememberEmployeeId(person ? person.id : null);
  applyPerson(person);
}

async function start() {
  who.addEventListener('change', onPick);

  try {
    const loaded = await loadPicker(who);
    employees = loaded.employees;
    setText(loadError, '');
    applyPerson(loaded.current);
  } catch (error) {
    setText(loadError, `The employee list could not be loaded. ${messageOf(error)}`);
    applyPerson(null);
  }
}

start();
