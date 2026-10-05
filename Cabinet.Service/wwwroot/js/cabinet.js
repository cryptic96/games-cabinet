/**
 * Entry point of the cabinet page: fetches the layout for the mount's sample and hands it to the renderer.
 */
import { renderCabinet } from './render.js';
import { COPY } from './copy.js';

const mount = document.getElementById('cabinet');

/**
 * Replaces the mount's content with a single status line.
 * @param {string} text The message to show.
 */
function showMessage(text) {
  const message = document.createElement('p');
  message.className = 'cabinet-message';
  message.textContent = text;
  mount.replaceChildren(message);
}

/**
 * Shows the load error with a button that repeats the load.
 */
function showError() {
  const heading = document.createElement('p');
  heading.className = 'cabinet-message';
  heading.textContent = COPY.errorHeading;

  const body = document.createElement('p');
  body.className = 'cabinet-message';
  body.textContent = COPY.errorBody;

  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'cabinet-retry';
  retry.textContent = COPY.retry;
  retry.addEventListener('click', load);

  mount.replaceChildren(heading, body, retry);
}

/**
 * Fetches and draws the cabinet, showing the loading line first and the error state on any failure.
 * @returns {Promise<void>}
 */
async function load() {
  showMessage(COPY.loading);

  try {
    const url = '/cabinet/layout?sample=' + encodeURIComponent(mount.dataset.sample) + '&profile=desktop';
    const response = await fetch(url);

    if (!response.ok) {
      showError();
      return;
    }

    renderCabinet(mount, await response.json(), COPY);
  } catch {
    showError();
  }
}

if (mount !== null) {
  load();
}
