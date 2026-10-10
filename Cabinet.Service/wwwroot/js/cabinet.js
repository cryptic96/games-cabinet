/**
 * Entry point of the cabinet page: fetches the layout for the viewport's profile (and the mount's sample when it carries one), hands it to the
 * renderer, and shows loading and error states. The profile follows one media query; nothing else listens to resize.
 */
import './language.js';
import { renderCabinet } from './render.js';
import { COPY } from './copy.js';
import { initSyncStatus, fetchStatus } from './sync.js';
import { startLive } from './live.js';

const mount = document.getElementById('cabinet');
const syncRoot = document.querySelector('.sync');
const phoneQuery = window.matchMedia('(max-width: 40rem)');

let latestLoad = 0;

/**
 * Returns the profile name the layout endpoint expects for the current viewport.
 * @returns {string}
 */
function currentProfile() {
  return phoneQuery.matches ? 'phone' : 'desktop';
}

/**
 * Builds the layout address for the current profile and the mount's sample, if it carries one.
 * @returns {string}
 */
function layoutUrl() {
  const sample = mount.dataset.sample;

  return '/cabinet/layout?profile=' + currentProfile() + (sample ? '&sample=' + encodeURIComponent(sample) : '');
}

/**
 * Replaces the mount's content with the loading line.
 */
function showLoading() {
  const message = document.createElement('p');
  message.className = 'cabinet-message cabinet-loading';
  message.textContent = COPY.loading;
  mount.replaceChildren(message);
}

/**
 * Shows the load error with a button that repeats the load.
 */
function showError() {
  const heading = document.createElement('p');
  heading.className = 'cabinet-message-heading';
  heading.textContent = COPY.errorHeading;

  const body = document.createElement('p');
  body.textContent = COPY.errorBody;

  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'cabinet-retry';
  retry.textContent = COPY.retry;
  retry.addEventListener('click', load);

  const message = document.createElement('div');
  message.className = 'cabinet-message';
  message.append(heading, body, retry);
  mount.replaceChildren(message);
}

/**
 * Fetches and draws the cabinet, showing the loading line first and the error state on any failure. A newer load
 * supersedes an older one that is still in flight, so a slow earlier response never overwrites a later one.
 * @returns {Promise<void>}
 */
async function load() {
  latestLoad += 1;
  const thisLoad = latestLoad;
  showLoading();

  try {
    const response = await fetch(layoutUrl());

    if (thisLoad !== latestLoad) {
      return;
    }

    if (!response.ok) {
      showError();
      return;
    }

    const layout = await response.json();

    if (thisLoad !== latestLoad) {
      return;
    }

    renderCabinet(mount, layout, COPY);
  } catch {
    if (thisLoad === latestLoad) {
      showError();
    }
  }
}

/**
 * Fetches the layout for the current profile and the mount's sample, if it carries one.
 * @returns {Promise<object | null>} The layout, or null when the answer is not a success.
 */
async function fetchLayout() {
  const response = await fetch(layoutUrl());

  return response.ok ? response.json() : null;
}

/**
 * Waits until the tab is visible, so a hidden tab redraws once when it comes back instead of while nobody looks.
 * @returns {Promise<void>}
 */
function whenVisible() {
  if (!document.hidden) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    document.addEventListener('visibilitychange', () => resolve(), { once: true });
  });
}

/**
 * Redraws the cabinet quietly after the collection changed: no loading line, no error state, no animation. The old cabinet stays
 * until the new layout is ready and is swapped for it in one step, together with the being-filled message. Keyboard focus returns
 * to the box with the same entry id when it still exists. Any failure leaves the old cabinet and says nothing. A newer load or
 * redraw supersedes this one.
 * @returns {Promise<boolean>} True when the new cabinet is on screen; false when nothing changed on screen.
 */
async function redraw() {
  await whenVisible();

  latestLoad += 1;
  const thisRedraw = latestLoad;

  try {
    const layout = await fetchLayout();

    if (thisRedraw !== latestLoad) {
      return false;
    }

    if (layout === null) {
      return abandonRedraw();
    }

    const focused = mount.contains(document.activeElement) ? document.activeElement.dataset.entryId : undefined;

    renderCabinet(mount, layout, COPY);

    const filling = document.querySelector('.cabinet-filling');

    if (filling !== null) {
      filling.remove();
    }

    if (focused !== undefined) {
      const same = mount.querySelector('[data-entry-id="' + CSS.escape(focused) + '"]');

      if (same !== null) {
        same.focus({ preventScroll: true });
      } else {
        document.activeElement.blur();
      }
    }

    return true;
  } catch {
    return thisRedraw === latestLoad ? abandonRedraw() : false;
  }
}

/**
 * Ends a redraw that could not get a layout without saying anything. If it had superseded a first load that was still showing
 * the loading line, that load is repeated so the line cannot stay on screen for good.
 * @returns {boolean} False: nothing changed on screen.
 */
function abandonRedraw() {
  if (mount.querySelector('.cabinet-loading') !== null) {
    load();
  }

  return false;
}

if (syncRoot !== null) {
  const sync = initSyncStatus(syncRoot, { onCollectionChanged: redraw });

  startLive({ applyStatus: sync.applyStatus, fetchStatus });
}

if (mount !== null) {
  phoneQuery.addEventListener('change', load);
  load();
}
