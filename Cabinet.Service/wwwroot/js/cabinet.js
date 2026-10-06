/**
 * Entry point of the cabinet page: fetches the layout for the viewport's profile (and the mount's sample when it carries one), hands it to the
 * renderer, and shows loading and error states. The profile follows one media query; nothing else listens to resize.
 */
import { renderCabinet } from './render.js';
import { COPY } from './copy.js';

const mount = document.getElementById('cabinet');
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
 * Replaces the mount's content with the loading line.
 */
function showLoading() {
  const message = document.createElement('p');
  message.className = 'cabinet-message';
  message.setAttribute('role', 'status');
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
    const sample = mount.dataset.sample;
    const url = '/cabinet/layout?profile=' + currentProfile() + (sample ? '&sample=' + encodeURIComponent(sample) : '');
    const response = await fetch(url);

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

if (mount !== null) {
  phoneQuery.addEventListener('change', load);
  load();
}
