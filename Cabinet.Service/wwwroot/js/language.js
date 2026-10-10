/**
 * The script side of the language toggle. A plain click on the link of the other language asks the server to remember the choice
 * and then reloads the same address, so the scroll position and any sample in the address survive. The request does not follow the
 * redirect: the browser stores the cookie from the redirect answer itself, and the reload fetches the page in the new language.
 * When the request fails for any reason the click follows the link instead, so the switch works without this script too.
 */

/**
 * Tells whether a click is an ordinary primary-button click, so a new-tab or download gesture is left to the browser.
 * @param {MouseEvent} event The click.
 * @returns {boolean}
 */
function isPlainClick(event) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
}

/**
 * Wires the toggle so that clicking the other language switches in place. Does nothing when there is no toggle.
 * @param {Element | null | undefined} toggle The element holding the language links.
 * @param {{ fetch?: Function, reload?: Function, assign?: Function }} [actions] The browser actions, replaceable in tests.
 */
export function initLanguageToggle(toggle, actions = {}) {
  if (!toggle) {
    return;
  }

  const send = actions.fetch ?? ((address, options) => globalThis.fetch(address, options));
  const reload = actions.reload ?? (() => globalThis.location.reload());
  const assign = actions.assign ?? ((address) => globalThis.location.assign(address));

  toggle.addEventListener('click', async (event) => {
    const link = event.target?.closest?.('a');

    if (!link || event.defaultPrevented || !isPlainClick(event) || link.getAttribute('aria-current') === 'true') {
      return;
    }

    event.preventDefault();

    try {
      const response = await send(link.href, { credentials: 'same-origin', cache: 'no-store', redirect: 'manual' });

      if (response.type !== 'opaqueredirect' && !response.ok) {
        assign(link.href);
        return;
      }

      reload();
    } catch {
      assign(link.href);
    }
  });
}

initLanguageToggle(globalThis.document?.querySelector?.('.lang-toggle'));
