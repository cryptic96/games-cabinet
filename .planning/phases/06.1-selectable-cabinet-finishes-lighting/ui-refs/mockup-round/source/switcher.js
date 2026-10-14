/**
 * Switcher for the mock-up round. A classic script, so the page works when opened straight from disk. It sets the five
 * look attributes on the root element, the evening marker the head script would set, and the lit marker that fades the
 * lights on, and it keeps the choice in the address hash so a state can be linked to or shot directly.
 */
(function () {
  var root = document.documentElement;
  var KEYS = ['design', 'wood', 'backs', 'lights', 'depth', 'dim', 'join'];
  var DEFAULTS = { design: 'classic', wood: 'brown', backs: 'wood', lights: 'off', depth: 'shaded', dim: 'off', join: 'off', evening: '0' };
  var state = {};
  var instant = false;

  function readHash() {
    var found = {};
    var text = location.hash.replace(/^#/, '');
    text.split('&').forEach(function (pair) {
      var parts = pair.split('=');
      if (parts[0]) {
        found[parts[0]] = decodeURIComponent(parts[1] || '');
      }
    });
    return found;
  }

  function writeHash() {
    var parts = [];
    Object.keys(DEFAULTS).forEach(function (key) {
      parts.push(key + '=' + state[key]);
    });
    history.replaceState(null, '', '#' + parts.join('&'));
  }

  function lit() {
    return state.lights === 'on' || (state.lights === 'evenings' && state.evening === '1');
  }

  function applyLit() {
    var shouldBeLit = lit();
    var isLit = root.hasAttribute('data-lit');

    if (shouldBeLit === isLit) {
      return;
    }

    if (!shouldBeLit) {
      root.removeAttribute('data-lit');
      return;
    }

    if (instant) {
      root.setAttribute('data-lit', '');
      return;
    }

    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        if (lit()) {
          root.setAttribute('data-lit', '');
        }
      });
    });
  }

  function paintButtons() {
    var buttons = document.querySelectorAll('.switcher button[data-key]');
    for (var i = 0; i < buttons.length; i++) {
      var b = buttons[i];
      b.setAttribute('aria-pressed', String(state[b.getAttribute('data-key')] === b.getAttribute('data-value')));
    }
    var evening = document.querySelector('.switcher fieldset[data-group="evening"]');
    if (evening) {
      evening.disabled = state.lights !== 'evenings';
    }
  }

  function swapLogo() {
    var logo = document.getElementById('bgg-logo');
    if (logo) {
      logo.setAttribute('src', state.wood === 'birch' ? 'img/bgg-dark-standin.svg' : 'img/powered-by-bgg.svg');
    }
  }

  function markPositions() {
    var sections = document.querySelectorAll('#cabinet .section');
    for (var i = 0; i < sections.length; i++) {
      var position = sections.length === 1 ? 'only' : i === 0 ? 'first' : i === sections.length - 1 ? 'last' : 'middle';
      sections[i].setAttribute('data-position', position);
    }
  }

  function apply() {
    KEYS.forEach(function (key) {
      root.setAttribute('data-' + key, state[key]);
    });
    if (state.evening === '1') {
      root.setAttribute('data-evening', '');
    } else {
      root.removeAttribute('data-evening');
    }
    applyLit();
    paintButtons();
    swapLogo();
    writeHash();
  }

  var fromHash = readHash();
  Object.keys(DEFAULTS).forEach(function (key) {
    state[key] = fromHash[key] !== undefined ? fromHash[key] : DEFAULTS[key];
  });
  instant = fromHash.settle === '0';
  if (fromHash.bare === '1') {
    root.setAttribute('data-bare', '');
  }

  document.querySelector('.switcher').addEventListener('click', function (event) {
    var b = event.target.closest('button[data-key]');
    if (!b) {
      return;
    }
    state[b.getAttribute('data-key')] = b.getAttribute('data-value');
    apply();
  });

  markPositions();
  apply();
  if (!instant) {
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        root.setAttribute('data-settled', '');
      });
    });
  }
  root.setAttribute('data-ready', '');
}());
