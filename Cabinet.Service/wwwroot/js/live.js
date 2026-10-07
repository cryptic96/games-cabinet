/**
 * Keeps an open page current without a reload: connects to the live channel when the browser client is present and reports every
 * pushed status to the page, fetches the status once whenever something may have been missed (a connect, a reconnect, the tab
 * becoming visible, the browser coming back online), and checks it once a minute while the tab is visible and the channel is not
 * connected. Nothing here is ever shown to the visitor: a lost connection is not an event the page talks about.
 */
import { reconnectDelayMs } from './status.js';

const LIVE_ROUTE = '/cabinet/live';
const FALLBACK_INTERVAL_MS = 60000;
const STABLE_CONNECTION_MS = 60000;

/**
 * Waits for the given time.
 * @param {number} milliseconds How long to wait.
 * @returns {Promise<void>}
 */
function pause(milliseconds) {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

/**
 * Starts the live behaviour. Without the browser client on the page it runs the status checks alone.
 * @param {{ applyStatus: (status: object) => void, fetchStatus: () => Promise<object | null>, random?: () => number }} handlers
 *   applyStatus takes every pushed or fetched status; fetchStatus asks the server for the current one and answers null when it
 *   cannot be had; random is the source of numbers in [0, 1) that spreads the reconnect waits (Math.random unless a test hands in its own).
 * @returns {void}
 */
export function startLive({ applyStatus, fetchStatus, random = Math.random }) {
  let connected = false;
  let catchingUp = false;

  /**
   * Fetches the status once and applies it; a failure or an overlapping check changes nothing.
   * @returns {Promise<void>}
   */
  async function catchUp() {
    if (catchingUp) {
      return;
    }

    catchingUp = true;

    try {
      const status = await fetchStatus();

      if (status !== null && typeof status === 'object') {
        applyStatus(status);
      }
    } catch {
      return;
    } finally {
      catchingUp = false;
    }
  }

  /**
   * Connects, and when the connection ends or never starts, tries again on the schedule: a moment later, then about 2 s, 10 s,
   * 30 s, then about every 60 s, each spread at random. The count starts over only after a connection has stayed up for a minute,
   * so a channel that opens and is closed at once (a page the server turned away) is retried more and more gently instead of in a
   * tight loop.
   * @param {object} signalR The browser client.
   * @returns {Promise<void>}
   */
  async function keepConnected(signalR) {
    let closed = null;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(LIVE_ROUTE)
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => reconnectDelayMs(context.previousRetryCount, random) })
      .configureLogging(signalR.LogLevel.None)
      .build();

    connection.on('statusChanged', (status) => {
      if (status !== null && typeof status === 'object') {
        applyStatus(status);
      }
    });
    connection.onreconnecting(() => {
      connected = false;
    });
    connection.onreconnected(() => {
      connected = true;
      catchUp();
    });
    connection.onclose(() => {
      connected = false;

      if (closed !== null) {
        closed();
      }
    });

    let failures = 0;

    for (;;) {
      try {
        const ended = new Promise((resolve) => {
          closed = resolve;
        });

        await connection.start();

        const upSince = Date.now();

        connected = true;
        catchUp();
        await ended;

        if (Date.now() - upSince >= STABLE_CONNECTION_MS) {
          failures = 0;
        }
      } catch {
        connected = false;
      }

      await pause(reconnectDelayMs(failures, random));
      failures += 1;
    }
  }

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      catchUp();
    }
  });
  window.addEventListener('online', catchUp);
  window.setInterval(() => {
    if (document.visibilityState === 'visible' && !connected) {
      catchUp();
    }
  }, FALLBACK_INTERVAL_MS);

  if (globalThis.signalR !== undefined) {
    keepConnected(globalThis.signalR);
  }
}
