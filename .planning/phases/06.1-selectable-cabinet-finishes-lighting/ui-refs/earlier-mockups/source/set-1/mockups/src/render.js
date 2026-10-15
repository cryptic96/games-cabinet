/**
 * Mockup renderer. It mirrors the production renderer (geometry only through custom properties set with
 * style.setProperty, text only through textContent) and adds the furniture hooks the finishes need:
 * three decorative elements per section (carcass, top, base) and a data-row-end flag on cubbies whose
 * right edge meets the side panel. Everything else about the look lives in the stylesheets.
 */
(function () {
  const PALETTE = [
    ['#8a2432', '#ffffff'], ['#1f5a3a', '#ffffff'], ['#23407a', '#ffffff'], ['#e0a82e', '#2a1a10'],
    ['#17766f', '#ffffff'], ['#6a2f7a', '#ffffff'], ['#b8481a', '#ffffff'], ['#8fbfe0', '#2a1a10'],
    ['#efe3c2', '#2a1a10'], ['#a3b88f', '#2a1a10'], ['#434f5e', '#ffffff'], ['#e09aa5', '#2a1a10'],
  ];

  /**
   * Stable stand-in tone per game, from the game id only.
   * @param {number} id The game id.
   * @returns {number}
   */
  function toneFor(id) {
    let h = Math.imul(id, 2654435761) >>> 0;
    h ^= h >>> 15;
    h = Math.imul(h, 2246822519) >>> 0;
    h ^= h >>> 13;
    return h % PALETTE.length;
  }

  /**
   * Sets one custom property.
   * @param {HTMLElement} element The element.
   * @param {string} name The property name.
   * @param {number | string} value The value.
   */
  function set(element, name, value) {
    element.style.setProperty(name, String(value));
  }

  /**
   * Builds a decorative, hidden element.
   * @param {string} className The class name.
   * @returns {HTMLDivElement}
   */
  function decor(className) {
    const element = document.createElement('div');
    element.className = className;
    element.setAttribute('aria-hidden', 'true');
    return element;
  }

  /**
   * Builds one placement button.
   * @param {object} placement The placement.
   * @returns {HTMLButtonElement}
   */
  function buildPlacement(placement) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'placement';
    button.dataset.kind = placement.kind;
    button.dataset.gameId = String(placement.gameId);
    set(button, '--x', placement.xMm);
    set(button, '--y', placement.yMm);
    set(button, '--w', placement.widthMm);
    set(button, '--h', placement.heightMm);
    const [bg, fg] = PALETTE[toneFor(placement.gameId)];
    set(button, '--bg', bg);
    set(button, '--fg', fg);
    button.setAttribute('aria-label', placement.title);
    button.title = placement.title;
    const label = document.createElement('span');
    label.className = 'placement-label';
    label.textContent = placement.label;
    button.append(label);
    return button;
  }

  /**
   * Builds one cubby.
   * @param {object} cubby The cubby.
   * @param {number} interiorWidth The section interior width in millimetres.
   * @returns {HTMLDivElement}
   */
  function buildCubby(cubby, interiorWidth) {
    const element = document.createElement('div');
    element.className = 'cubby';
    set(element, '--x', cubby.xMm);
    set(element, '--y', cubby.yMm);
    set(element, '--w', cubby.widthMm);
    set(element, '--h', cubby.heightMm);
    if (cubby.xMm + cubby.widthMm >= interiorWidth) {
      element.dataset.rowEnd = '';
    }
    element.append(...cubby.placements.map(buildPlacement));
    return element;
  }

  /**
   * Builds one section.
   * @param {object} section The section.
   * @returns {HTMLDivElement}
   */
  function buildSection(section) {
    const element = document.createElement('div');
    element.className = 'section';
    set(element, '--section-w', section.widthMm + 2 * section.frameMm);
    set(element, '--section-h', section.heightMm + 2 * section.frameMm);
    set(element, '--frame', section.frameMm);
    const body = document.createElement('div');
    body.className = 'section-body';
    body.append(decor('cabinet-top'), decor('carcass'), decor('cabinet-base'));
    body.append(...section.cubbies.map((cubby) => buildCubby(cubby, section.widthMm)));
    element.append(body);
    return element;
  }

  const phone = window.matchMedia('(max-width: 40rem)').matches;
  const layout = phone ? window.CABINET_LAYOUT_PHONE : window.CABINET_LAYOUT;
  const mount = document.getElementById('cabinet');
  mount.replaceChildren(...layout.sections.map(buildSection));
  document.documentElement.dataset.profile = phone ? 'phone' : 'desktop';
})();
