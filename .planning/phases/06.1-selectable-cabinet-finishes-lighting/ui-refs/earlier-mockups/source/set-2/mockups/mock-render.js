/**
 * Mockup copy of Cabinet.Service/wwwroot/js/render.js. It builds the same DOM (section > section-body > cubby >
 * placement) and sets geometry the same way (custom properties through element.style.setProperty, text through
 * textContent), so every finish in cabinet-finishes.css could be pasted into cabinet.css without touching the renderer.
 *
 * The only addition is a box colour per game: the engine sends toneIndex 0 for every game today, so the mockup picks a
 * tone from the planned placeholder palette by hashing the game id. That is only there so the furniture can be judged
 * next to colourful boxes; it is identical in every finish and is not part of the furniture proposal.
 */
(function () {
  const PALETTE = [
    ['#8a2432', '#ffffff'], ['#1f5a3a', '#ffffff'], ['#23407a', '#ffffff'], ['#e0a82e', '#2a1a10'],
    ['#17766f', '#ffffff'], ['#6a2f7a', '#ffffff'], ['#b8481a', '#ffffff'], ['#8fbfe0', '#2a1a10'],
    ['#efe3c2', '#2a1a10'], ['#a3b88f', '#2a1a10'], ['#434f5e', '#ffffff'], ['#e09aa5', '#2a1a10'],
  ];

  /** Sets one custom property to a plain number. */
  function setNumber(element, name, value) {
    element.style.setProperty(name, String(value));
  }

  /** Stable tone for one game, from its own id only. */
  function toneFor(gameId) {
    return (Math.imul(gameId, 2654435761) >>> 0) % PALETTE.length;
  }

  /** Builds one placement button. */
  function buildPlacement(placement, options) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'placement';
    button.dataset.kind = placement.kind;
    button.dataset.gameId = String(placement.gameId);
    setNumber(button, '--x', placement.xMm);
    setNumber(button, '--y', placement.yMm);
    setNumber(button, '--w', placement.widthMm);
    setNumber(button, '--h', placement.heightMm);

    if (options.boxes === 'palette') {
      const [bg, fg] = PALETTE[toneFor(placement.gameId)];
      button.style.setProperty('--bg', bg);
      button.style.setProperty('--fg', fg);
    }

    const title = placement.title && placement.title.trim() !== '' ? placement.title : 'Untitled game';
    button.setAttribute('aria-label', title);
    button.title = title;
    const label = document.createElement('span');
    label.className = 'placement-label';
    label.textContent = placement.label && placement.label.trim() !== '' ? placement.label : 'Untitled game';
    button.append(label);

    return button;
  }

  /** Builds one cubby with its placements. */
  function buildCubby(cubby, options) {
    const element = document.createElement('div');
    element.className = 'cubby';
    setNumber(element, '--x', cubby.xMm);
    setNumber(element, '--y', cubby.yMm);
    setNumber(element, '--w', cubby.widthMm);
    setNumber(element, '--h', cubby.heightMm);
    element.append(...cubby.placements.map((placement) => buildPlacement(placement, options)));

    return element;
  }

  /** Builds one section. */
  function buildSection(section, options) {
    const element = document.createElement('div');
    element.className = 'section';
    setNumber(element, '--section-w', section.widthMm + 2 * section.frameMm);
    setNumber(element, '--section-h', section.heightMm + 2 * section.frameMm);
    setNumber(element, '--frame', section.frameMm);

    const body = document.createElement('div');
    body.className = 'section-body';
    body.append(...section.cubbies.map((cubby) => buildCubby(cubby, options)));
    element.append(body);

    return element;
  }

  /**
   * Replaces the content of the mount with the drawn cabinet.
   * options.sections limits which section indexes are drawn (all when omitted).
   */
  window.renderMockCabinet = function (mount, layout, options) {
    const sections = options.sections
      ? layout.sections.filter((section) => options.sections.includes(section.index))
      : layout.sections;
    mount.replaceChildren(...sections.map((section) => buildSection(section, options)));
  };
})();
