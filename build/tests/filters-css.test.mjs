/**
 * Checks the structure rules of the filter style sheet with the plain Node test runner: the sheet is read from disk as text and its
 * rules are looked at one by one. These are the promises a later edit must not break: the sheet writes no colour of its own, uses no
 * blur and no filter, never reaches into the cabinet's boxes, moves things only with opacity and transform, keeps the bar at its level,
 * never sets muted text on a wash or a chosen fill, and leaves the shadow of a dimmed box to the modifiers layer. Across every sheet
 * the cabinet page loads, nothing that applies from 64rem makes the dock, the body or the root element the containing block of the
 * fixed panel that flies over the cabinet, so that panel is always laid out against the viewport.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const CSS_FOLDER = '../../Cabinet.Service/wwwroot/css/';
const NUMERIC_COLOUR = /\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(\s*[-\d.]/;
const HEX_COLOUR = /#[0-9a-fA-F]{3,8}\b/;
const ALLOWED_MOTION = new Set(['opacity', 'transform', 'translate']);
const NAMED_COLOURS = new Set(
  (
    'aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue blueviolet brown burlywood cadetblue ' +
    'chartreuse chocolate coral cornflowerblue cornsilk crimson cyan darkblue darkcyan darkgoldenrod darkgray darkgreen darkgrey ' +
    'darkkhaki darkmagenta darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue darkslategray ' +
    'darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray dimgrey dodgerblue firebrick floralwhite forestgreen ' +
    'fuchsia gainsboro ghostwhite gold goldenrod gray green greenyellow grey honeydew hotpink indianred indigo ivory khaki lavender ' +
    'lavenderblush lawngreen lemonchiffon lightblue lightcoral lightcyan lightgoldenrodyellow lightgray lightgreen lightgrey ' +
    'lightpink lightsalmon lightseagreen lightskyblue lightslategray lightslategrey lightsteelblue lightyellow lime limegreen linen ' +
    'magenta maroon mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue mediumspringgreen ' +
    'mediumturquoise mediumvioletred midnightblue mintcream mistyrose moccasin navajowhite navy oldlace olive olivedrab orange ' +
    'orangered orchid palegoldenrod palegreen paleturquoise palevioletred papayawhip peachpuff peru pink plum powderblue purple ' +
    'rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown seagreen seashell sienna silver skyblue slateblue ' +
    'slategray slategrey snow springgreen steelblue tan teal thistle tomato turquoise violet wheat white whitesmoke yellow ' +
    'yellowgreen'
  ).split(' '),
);

/**
 * Removes the comments of a style sheet, so a word that only a comment mentions is not mistaken for a rule.
 * @param {string} text The style sheet.
 * @returns {string}
 */
function withoutComments(text) {
  const open = '/' + '*';
  const close = '*' + '/';
  let result = text;

  for (let start = result.indexOf(open); start !== -1; start = result.indexOf(open)) {
    const end = result.indexOf(close, start + open.length);

    result = result.slice(0, start) + (end === -1 ? '' : result.slice(end + close.length));
  }

  return result;
}

/**
 * Reads a style sheet of the site without its comments.
 * @param {string} name The file name inside the style folder.
 * @returns {string}
 */
function readCss(name) {
  return withoutComments(readFileSync(new URL(`${CSS_FOLDER}${name}`, import.meta.url), 'utf8'));
}

/**
 * Splits a block of style text into its top-level items: statements that end in a semicolon and blocks with a body.
 * @param {string} text The style text.
 * @returns {{ prelude: string, body: string | null }[]}
 */
function items(text) {
  const found = [];
  let depth = 0;
  let start = 0;
  let bodyStart = -1;
  let parens = 0;

  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];

    if (character === '(') {
      parens += 1;
    } else if (character === ')') {
      parens -= 1;
    } else if (character === '{' && parens === 0) {
      if (depth === 0) {
        bodyStart = index;
      }

      depth += 1;
    } else if (character === '}' && parens === 0) {
      depth -= 1;

      if (depth === 0) {
        found.push({ prelude: text.slice(start, bodyStart).trim(), body: text.slice(bodyStart + 1, index) });
        start = index + 1;
      }
    } else if (character === ';' && depth === 0 && parens === 0) {
      found.push({ prelude: text.slice(start, index).trim(), body: null });
      start = index + 1;
    }
  }

  return found.filter((item) => item.prelude !== '' || item.body !== null);
}

/**
 * Splits the declarations of a rule body into name and value pairs.
 * @param {string} body The text between the braces of a rule.
 * @returns {{ name: string, value: string }[]}
 */
function declarations(body) {
  const pairs = [];
  let parens = 0;
  let start = 0;

  for (let index = 0; index <= body.length; index += 1) {
    const character = body[index];

    if (character === '(') {
      parens += 1;
    } else if (character === ')') {
      parens -= 1;
    } else if ((character === ';' && parens === 0) || index === body.length) {
      const text = body.slice(start, index).trim();
      const colon = text.indexOf(':');

      if (colon > 0) {
        pairs.push({ name: text.slice(0, colon).trim(), value: text.slice(colon + 1).trim() });
      }

      start = index + 1;
    }
  }

  return pairs;
}

/**
 * Lists every rule of a block of style text with the selector it answers to, the layer it sits in and its declarations, looking
 * inside media, container, supports, starting-style and layer blocks.
 * @param {string} text The style text.
 * @param {string | null} layer The layer the text sits in, or null for none.
 * @returns {{ selector: string, layer: string | null, declarations: { name: string, value: string }[] }[]}
 */
function rules(text, layer = null) {
  const found = [];

  for (const item of items(text)) {
    if (item.body === null) {
      continue;
    }

    const layerName = /^@layer\s+([\w-]+)$/.exec(item.prelude);

    if (/^@(media|container|supports|layer|starting-style)\b/.test(item.prelude)) {
      found.push(...rules(item.body, layerName === null ? layer : layerName[1]));
    } else if (!item.prelude.startsWith('@')) {
      found.push({ selector: item.prelude, layer, declarations: declarations(item.body) });
    }
  }

  return found;
}

/**
 * Lists every selector of a rule: a comma outside brackets separates them.
 * @param {string} selector The selector list of a rule.
 * @returns {string[]}
 */
function selectorsOf(selector) {
  const result = [];
  let depth = 0;
  let start = 0;

  for (let index = 0; index <= selector.length; index += 1) {
    const character = selector[index];

    if (character === '(' || character === '[') {
      depth += 1;
    } else if (character === ')' || character === ']') {
      depth -= 1;
    } else if ((character === ',' && depth === 0) || index === selector.length) {
      result.push(selector.slice(start, index).trim());
      start = index + 1;
    }
  }

  return result.filter((entry) => entry !== '');
}

/**
 * Removes every custom property reference and name from a value, so a word inside a variable is not mistaken for a colour.
 * @param {string} value The declaration value.
 * @returns {string}
 */
function withoutVariables(value) {
  return value.replace(/var\([^()]*(\([^()]*\)[^()]*)*\)/g, ' ').replace(/--[\w-]+/g, ' ');
}

/**
 * Finds the colour literals in a declaration value: hexadecimal values, functions with numeric channels and named colours. A
 * fallback inside a variable reference is not looked at, and neither are the system colours forced colours use.
 * @param {string} value The declaration value.
 * @returns {string[]}
 */
function colourLiterals(value) {
  const cleaned = withoutVariables(value);
  const literals = [];

  if (HEX_COLOUR.test(cleaned)) {
    literals.push(cleaned.match(HEX_COLOUR)[0]);
  }

  for (const match of cleaned.matchAll(new RegExp(NUMERIC_COLOUR.source, 'g'))) {
    literals.push(match[0]);
  }

  for (const word of cleaned.match(/[a-zA-Z][\w-]*/g) ?? []) {
    if (NAMED_COLOURS.has(word.toLowerCase())) {
      literals.push(word);
    }
  }

  return literals;
}

/**
 * Tells whether a transition value moves only things that cost the compositor nothing: every property it names is opacity,
 * transform or translate, or is a visibility switch that takes no time of its own and only waits for a slide to finish.
 * @param {string} value The value of a transition declaration.
 * @returns {string[]} The properties that break the rule.
 */
function badMotion(value) {
  const bad = [];

  for (const part of value.split(/,(?![^(]*\))/)) {
    const words = withoutVariables(part).trim().split(/\s+/);
    const property = words[0];
    const duration = words[1];

    if (ALLOWED_MOTION.has(property)) {
      continue;
    }

    if (property === 'visibility' && duration === '0s') {
      continue;
    }

    bad.push(property);
  }

  return bad;
}

/**
 * Strips the state pseudo-classes of a selector, so a hover or a chosen state is matched to the element it styles.
 * @param {string} selector One selector.
 * @returns {string}
 */
function baseSelector(selector) {
  let result = selector.replace(/:hover\b/g, '');

  for (let start = result.indexOf(':has('); start !== -1; start = result.indexOf(':has(')) {
    let depth = 0;
    let end = start + ':has'.length;

    for (; end < result.length; end += 1) {
      depth += result[end] === '(' ? 1 : result[end] === ')' ? -1 : 0;

      if (depth === 0) {
        break;
      }
    }

    result = result.slice(0, start) + result.slice(end + 1);
  }

  return result.trim();
}

/**
 * Finds the value a rule gives to the first of the named properties it declares.
 * @param {{ declarations: { name: string, value: string }[] }} rule The rule.
 * @param {string[]} names The property names to look for.
 * @returns {string | null}
 */
function valueOf(rule, names) {
  return rule.declarations.find((declaration) => names.includes(declaration.name))?.value ?? null;
}

const filters = readCss('filters.css');
const filterRules = rules(filters);
const filterDeclarations = filterRules.flatMap((rule) => rule.declarations.map((declaration) => ({ ...declaration, selector: rule.selector })));

test('the filter sheet writes no colour of its own, only wall tokens, shades over a token and mixes of tokens', () => {
  assert.ok(filterDeclarations.length > 50, 'the sheet has rules');

  for (const { selector, name, value } of filterDeclarations) {
    assert.deepEqual(colourLiterals(value), [], `${selector} { ${name}: ${value} }`);
  }
});

test('the colour check recognises a literal when one is written and lets tokens through', () => {
  assert.equal(colourLiterals('#fff').length, 1);
  assert.equal(colourLiterals('rgb(1 2 3 / 0.5)').length, 1);
  assert.equal(colourLiterals('red').length, 1);
  assert.deepEqual(colourLiterals('rgb(var(--room-shade-rgb, 20 10 4) / 0.55)'), []);
  assert.deepEqual(colourLiterals('color-mix(in srgb, var(--room-text, var(--wall-text)) 8%, transparent)'), []);
  assert.deepEqual(colourLiterals('2px solid Highlight'), []);
});

test('the filter sheet uses no filter, no backdrop filter and no blur', () => {
  for (const { selector, name, value } of filterDeclarations) {
    assert.notEqual(name, 'filter', `${selector} sets filter`);
    assert.notEqual(name, 'backdrop-filter', `${selector} sets backdrop-filter`);
    assert.doesNotMatch(value, /blur\(/, `${selector} { ${name}: ${value} }`);
  }
});

test('no rule of the filter sheet is aimed at the cabinet or its boxes', () => {
  const aimed = /\.(cabinet|section|cubby|placement)(?![\w-])/;

  for (const rule of filterRules) {
    for (const selector of selectorsOf(rule.selector)) {
      assert.doesNotMatch(selector, aimed, `filters.css reaches into the cabinet with ${selector}`);
    }
  }
});

test('every transition of the filter sheet moves only opacity, transform or translate', () => {
  const transitions = filterDeclarations.filter(({ name }) => name === 'transition');

  assert.ok(transitions.length > 0, 'the sheet has transitions');

  for (const { selector, value } of transitions) {
    if (value !== 'none') {
      assert.deepEqual(badMotion(value), [], `${selector} { transition: ${value} }`);
    }
  }

  for (const { selector, value } of filterDeclarations.filter(({ name }) => name === 'transition-property')) {
    assert.deepEqual(badMotion(value), [], `${selector} { transition-property: ${value} }`);
  }
});

test('the motion check recognises a slow property and lets a waiting visibility switch through', () => {
  assert.deepEqual(badMotion('background 1s'), ['background']);
  assert.deepEqual(badMotion('transform 1s ease, visibility 0s linear 1s'), []);
  assert.deepEqual(badMotion('opacity var(--fade), height 1s'), ['height']);
  assert.deepEqual(badMotion('visibility 1s'), ['visibility']);
});

test('the bar sits at level five', () => {
  const bar = filterRules.filter((rule) => selectorsOf(rule.selector).includes('.filter-bar') && valueOf(rule, ['z-index']) !== null);

  assert.equal(bar.length, 1);
  assert.equal(valueOf(bar[0], ['z-index']), '5');
});

test('no rule sets muted text on a wash or on a chosen fill', () => {
  const fills = (value) => value !== null && /--room-(wash|text)\b/.test(withoutFallbacks(value));
  const muted = (value) => value !== null && /--room-muted\b/.test(value);
  const filled = filterRules.filter((rule) => fills(valueOf(rule, ['background', 'background-color'])));
  const mutedBases = new Set(
    filterRules
      .filter((rule) => muted(valueOf(rule, ['color'])))
      .flatMap((rule) => selectorsOf(rule.selector).map(baseSelector)),
  );

  assert.ok(filled.length > 0, 'the sheet has washes and fills');

  for (const rule of filled) {
    assert.equal(muted(valueOf(rule, ['color'])), false, `${rule.selector} sets muted text on its own fill`);

    for (const selector of selectorsOf(rule.selector)) {
      assert.equal(mutedBases.has(baseSelector(selector)), false, `${selector} is filled while its text is muted`);
    }
  }
});

test('the veil rules live in the modifiers layer of the cabinet sheet and nowhere else', () => {
  const sheets = ['cabinet.css', 'cabinet-design.css', 'cabinet-wood.css', 'cabinet-modifiers.css', 'filters.css', 'owner.css', 'card.css', 'site.css'];

  for (const name of sheets) {
    const veil = rules(readCss(name)).filter((rule) => /placement-veil/.test(rule.selector));

    if (name === 'cabinet-modifiers.css') {
      assert.ok(veil.length >= 3, 'the modifiers sheet holds the veil rules');

      for (const rule of veil) {
        assert.equal(rule.layer, 'modifiers', `${rule.selector} is outside the modifiers layer`);
      }
    } else {
      assert.deepEqual(veil.map((rule) => rule.selector), [], `${name} has a veil rule`);
    }
  }
});

/**
 * Removes the fallback of every variable reference, so a token named only as a fallback does not count.
 * @param {string} value The declaration value.
 * @returns {string}
 */
function withoutFallbacks(value) {
  return value.replace(/var\((--[\w-]+)\s*,\s*(?:[^()]|\([^()]*(?:\([^()]*\)[^()]*)*\))*\)/g, 'var($1)');
}

test('the view switch clips nothing and draws its keyboard ring two pixels outside the segment', () => {
  const clipping = filterRules.filter((rule) => selectorsOf(rule.selector).includes('.filter-view') && valueOf(rule, ['overflow']) === 'hidden');
  const ring = filterRules.find((rule) => rule.selector === '.filter-view-segment:has(input:focus-visible)');

  assert.deepEqual(clipping.map((rule) => rule.selector), []);
  assert.ok(ring, 'the segment has a keyboard ring rule');
  assert.equal(valueOf(ring, ['outline-offset']), '2px');
});

test('a state selector is matched to the element it styles', () => {
  assert.equal(baseSelector('.filter-chip:hover'), '.filter-chip');
  assert.equal(baseSelector('.filter-chip:has(input:checked:not([value=""]))'), '.filter-chip');
  assert.equal(baseSelector('.filter-view-segment:has(input:checked):hover'), '.filter-view-segment');
  assert.equal(withoutFallbacks('var(--room-wash, color-mix(in srgb, var(--room-text, red) 8%, transparent))'), 'var(--room-wash)');
});

/** The properties that make an element the containing block of a fixed descendant, or clip it, when they hold a real value. */
const CONTAINING_PROPERTIES = new Set([
  'transform',
  'translate',
  'rotate',
  'scale',
  'perspective',
  'filter',
  'backdrop-filter',
  'contain',
  'container-type',
  'container',
  'view-transition-name',
  'will-change',
]);

/** The values of those properties that change nothing about the containing block. */
const HARMLESS_VALUES = new Map([
  ['transform', ['none']],
  ['translate', ['none']],
  ['rotate', ['none']],
  ['scale', ['none']],
  ['perspective', ['none']],
  ['filter', ['none']],
  ['backdrop-filter', ['none']],
  ['contain', ['none', 'style']],
  ['container-type', ['normal']],
  ['view-transition-name', ['none']],
  ['will-change', ['auto']],
]);

/** Every style sheet the cabinet page can load. */
const PAGE_SHEETS = ['site.css', 'cabinet.css', 'cabinet-design.css', 'cabinet-wood.css', 'cabinet-modifiers.css', 'card.css', 'filters.css'];

/**
 * Lists every rule of a block of style text with its selector, its declarations and the at-rule preludes it sits inside.
 * @param {string} text The style text.
 * @param {string[]} conditions The preludes of the blocks around the text.
 * @returns {{ selector: string, conditions: string[], declarations: { name: string, value: string }[] }[]}
 */
function scopedRules(text, conditions = []) {
  const found = [];

  for (const item of items(text)) {
    if (item.body === null) {
      continue;
    }

    if (/^@(media|container|supports|layer|starting-style)\b/.test(item.prelude)) {
      found.push(...scopedRules(item.body, [...conditions, item.prelude]));
    } else if (!item.prelude.startsWith('@')) {
      found.push({ selector: item.prelude, conditions, declarations: declarations(item.body) });
    }
  }

  return found;
}

/**
 * Turns a length in a media query into pixels.
 * @param {string} number The number.
 * @param {string} unit `rem`, `em` or `px`.
 * @returns {number}
 */
function mediaPixels(number, unit) {
  return Number.parseFloat(number) * (unit === 'px' ? 1 : 16);
}

/**
 * Tells whether one media query of a list can only match below 64rem (1024px), where the page has no dock.
 * @param {string} query One media query.
 * @returns {boolean}
 */
function queryOnlyBelowDock(query) {
  const text = query.trim();
  const notWide = /^not\s+all\s+and\s+\(\s*min-width:\s*([\d.]+)(rem|em|px)\s*\)$/.exec(text);

  if (notWide !== null) {
    return mediaPixels(notWide[1], notWide[2]) <= 1024;
  }

  const below = /\(\s*width\s*<\s*([\d.]+)(rem|em|px)\s*\)/.exec(text);

  if (below !== null && mediaPixels(below[1], below[2]) <= 1024) {
    return true;
  }

  const upTo = /\(\s*(?:max-width:|width\s*<=)\s*([\d.]+)(rem|em|px)\s*\)/.exec(text);

  return upTo !== null && mediaPixels(upTo[1], upTo[2]) < 1024;
}

/**
 * Tells whether an at-rule prelude is a media block that can only apply below 64rem: every query of its list does.
 * @param {string} prelude The at-rule prelude.
 * @returns {boolean}
 */
function onlyBelowDock(prelude) {
  const media = /^@media\s+(.+)$/s.exec(prelude);

  return media !== null && selectorsOf(media[1]).every(queryOnlyBelowDock);
}

/**
 * Gives the subject of a selector: its last compound, the element the rule styles.
 * @param {string} selector One selector.
 * @returns {string}
 */
function subjectOf(selector) {
  const compounds = [];
  let depth = 0;
  let current = '';

  for (const character of selector.trim()) {
    if (character === '(' || character === '[') {
      depth += 1;
    } else if (character === ')' || character === ']') {
      depth -= 1;
    }

    if (depth === 0 && /[\s>+~]/.test(character)) {
      compounds.push(current);
      current = '';
    } else {
      current += character;
    }
  }

  compounds.push(current);

  return compounds.filter((compound) => compound !== '').at(-1) ?? '';
}

/**
 * Removes every negation and attribute value from a compound selector, so `:not(.filter-bar)` or `[data-x="body"]` is not read as
 * the dock or the body.
 * @param {string} compound A compound selector.
 * @returns {string}
 */
function withoutNegations(compound) {
  let result = compound;

  for (let start = result.indexOf(':not('); start !== -1; start = result.indexOf(':not(')) {
    let depth = 0;
    let end = start + ':not'.length;

    for (; end < result.length; end += 1) {
      depth += result[end] === '(' ? 1 : result[end] === ')' ? -1 : 0;

      if (depth === 0) {
        break;
      }
    }

    result = result.slice(0, start) + result.slice(end + 1);
  }

  return result.replace(/\[[^\]]*\]/g, '[]');
}

/**
 * Tells whether the subject of a selector can be the dock, `body` or `html`. A pseudo-element such as `::view-transition-group` is
 * not the element itself and does not count.
 * @param {string} subject The subject compound.
 * @returns {boolean}
 */
function isDockOrAncestor(subject) {
  if (subject.includes('::')) {
    return false;
  }

  const compound = withoutNegations(subject);

  return (
    /\.filter-bar(?![\w-])/.test(compound) ||
    /:root(?![\w-])/.test(compound) ||
    /(^|[(,\s])(html|body)(?![\w-])/.test(compound) ||
    compound.startsWith('*')
  );
}

/**
 * Tells whether a declaration makes its element a containing block for fixed descendants, or clips them.
 * @param {{ name: string, value: string }} declaration The declaration.
 * @returns {boolean}
 */
function makesContainingBlock({ name, value }) {
  const property = name.replace(/^-webkit-/, '');
  const cleaned = value.replace(/\s*!important\s*$/, '').trim();

  if (!CONTAINING_PROPERTIES.has(property)) {
    return false;
  }

  if (property === 'will-change') {
    return cleaned.split(',').some((part) => CONTAINING_PROPERTIES.has(part.trim().replace(/^-webkit-/, '')));
  }

  if (property === 'container') {
    const type = cleaned.split('/')[1]?.trim() ?? 'normal';

    return type !== 'normal';
  }

  return !(HARMLESS_VALUES.get(property) ?? []).includes(cleaned);
}

/**
 * Lists the rules of a style sheet that can apply from 64rem, match the dock, `body` or `html`, and make that element the
 * containing block of the fixed fly-over panel.
 * @param {string} text The style sheet without comments.
 * @returns {string[]} One line per offending declaration.
 */
function containingBlockOffenders(text) {
  const offenders = [];

  for (const rule of scopedRules(text)) {
    if (rule.conditions.some(onlyBelowDock)) {
      continue;
    }

    const subjects = selectorsOf(rule.selector).filter((selector) => isDockOrAncestor(subjectOf(selector)));

    for (const declaration of rule.declarations.filter(makesContainingBlock)) {
      for (const selector of subjects) {
        offenders.push(`${selector} { ${declaration.name}: ${declaration.value} }`);
      }
    }
  }

  return offenders;
}

test('nothing from 64rem makes the dock, body or html the containing block of the fly-over', () => {
  for (const name of PAGE_SHEETS) {
    assert.deepEqual(containingBlockOffenders(readCss(name)), [], `${name} traps the fixed panel`);
  }
});

test('the containing-block check finds such a rule and lets the panel, phones and pseudo-elements through', () => {
  const sample = `
    html[data-filter-dock] body { transform: translateZ(0); }
    @media (width < 64rem) { .filter-bar[data-away] { translate: 0 -100%; } }
    @media not all and (min-width: 64rem) { body { filter: blur(1px); } }
    @media (width < 64rem) and (prefers-reduced-motion: reduce) { .filter-bar { will-change: transform; } }
    .filter-bar .filter-panel { translate: -100% 0; }
    html[data-switching-view] .filter-panel { view-transition-name: filter-panel; }
    html::view-transition-group(root) { animation: none; }
    :root { will-change: transform, opacity; }
    .filter-bar { container: dock / inline-size; }
    .filter-bar-panel { contain: paint; }
    .filter-bar:not(.filter-bar) .x { contain: paint; }
    @media (min-width: 64rem) { @layer structure { .filter-bar { view-transition-name: dock; } } }
    body { transform: none; will-change: auto; container: page; }
    @media (min-width: 40rem) { html { -webkit-backdrop-filter: blur(2px); } }
  `;

  assert.deepEqual(containingBlockOffenders(sample), [
    'html[data-filter-dock] body { transform: translateZ(0) }',
    ':root { will-change: transform, opacity }',
    '.filter-bar { container: dock / inline-size }',
    '.filter-bar { view-transition-name: dock }',
    'html { -webkit-backdrop-filter: blur(2px) }',
  ]);
});

test('a selector is matched to its subject, and a media block below the dock width is told apart', () => {
  assert.equal(subjectOf('html[data-filter-dock] body > main'), 'main');
  assert.equal(subjectOf('.a:is(.b .c) ~ .filter-bar'), '.filter-bar');
  assert.equal(subjectOf('html[data-x="a b"]'), 'html[data-x="a b"]');
  assert.equal(onlyBelowDock('@media (width < 64rem)'), true);
  assert.equal(onlyBelowDock('@media (max-width: 40rem)'), true);
  assert.equal(onlyBelowDock('@media (min-width: 64rem)'), false);
  assert.equal(onlyBelowDock('@media (prefers-reduced-motion: reduce)'), false);
  assert.equal(onlyBelowDock('@media (width < 64rem), print'), false);
});

/** The hairline at the inline end of the rail and the panel: the line colour at 40% over nothing, a token blend and never a literal. */
const HAIRLINE = 'color-mix(in srgb, var(--room-line) 40%, transparent)';

/**
 * The rules of the filter sheet that only apply while the page has a desktop dock, in normal colours: under forced colours the
 * system draws the colours and the contrast pairs do not apply.
 */
const dockRules = scopedRules(filters).filter(
  (rule) => /html\[data-filter-dock/.test(rule.selector) && !rule.conditions.some((condition) => /forced-colors/.test(condition)),
);

/**
 * Splits a shadow value into its layers: a comma outside brackets separates them.
 * @param {string} value The value of a box-shadow declaration.
 * @returns {string[]}
 */
function shadowLayers(value) {
  return selectorsOf(value);
}

/**
 * Lists what is wrong with the hairlines a shadow value draws: every layer drawn as a one pixel line inside the inline end has to be
 * the line token blended at 40% over nothing, with no colour of its own.
 * @param {string} value The value of a box-shadow declaration.
 * @returns {{ hairlines: number, problems: string[] }}
 */
function hairlineCheck(value) {
  const problems = [];
  let hairlines = 0;

  for (const layer of shadowLayers(value)) {
    const inset = /^inset\s+-1px\s+0\s+0\s+(.+)$/s.exec(layer.trim());

    if (inset === null) {
      continue;
    }

    hairlines += 1;

    if (withoutFallbacks(inset[1]).trim() !== HAIRLINE) {
      problems.push(layer.trim());
    }
  }

  return { hairlines, problems };
}

/**
 * Lists the shadow layers of a value that are not the hairline and whose colour is anything but the wall's shade colour at an alpha.
 * @param {string} value The value of a box-shadow declaration.
 * @returns {string[]}
 */
function foreignShadows(value) {
  return shadowLayers(value)
    .filter((layer) => !/^inset\s+-1px\s+0\s+0\s/.test(layer.trim()))
    .filter((layer) => {
      const plain = withoutFallbacks(layer);
      const tokens = [...plain.matchAll(/var\((--[\w-]+)\)/g)].map((match) => match[1]);
      const colour = /rgb\(var\(--room-shade-rgb\)\s*\/\s*[\d.]+\)\s*$/.test(plain.trim());

      return !colour || tokens.some((token) => token !== '--room-shade-rgb') || colourLiterals(layer).length > 0;
    })
    .map((layer) => layer.trim());
}

/**
 * Finds the dock rule whose selector list holds exactly the given selector.
 * @param {string} selector One selector.
 * @returns {{ selector: string, declarations: { name: string, value: string }[] }[]}
 */
function dockRulesFor(selector) {
  return dockRules.filter((rule) => selectorsOf(rule.selector).includes(selector));
}

/**
 * Reads the value a group of dock rules gives a property, with every fallback removed, from the last rule that sets it.
 * @param {string} selector One selector.
 * @param {string[]} names The property names to look for.
 * @returns {string | null}
 */
function dockValue(selector, names) {
  const value = dockRulesFor(selector)
    .map((rule) => valueOf(rule, names))
    .filter((found) => found !== null)
    .at(-1);

  return value === undefined ? null : withoutFallbacks(value);
}

test('the dock rules are in the sheet, so every sheet-wide check above covers the rail and the panel', () => {
  assert.ok(dockRules.length > 30, 'the sheet has the dock rules');
  assert.ok(dockRules.some((rule) => /\.filter-rail(?![\w-])/.test(rule.selector)), 'the rail has rules');
  assert.ok(dockRules.some((rule) => /\.filter-panel(?![\w-])/.test(rule.selector)), 'the panel has rules');
});

test('the rail and the panel draw their edge as the token hairline and the fly-over shadow only in the wall shade colour', () => {
  const edges = [
    'html[data-filter-dock] .filter-rail',
    'html[data-filter-dock="push"] .filter-panel',
    'html[data-filter-dock="over"] .filter-panel',
  ];

  for (const selector of edges) {
    const shadow = dockRulesFor(selector)
      .map((rule) => valueOf(rule, ['box-shadow']))
      .filter((value) => value !== null)
      .at(-1);

    assert.ok(shadow, `${selector} draws its edge as a shadow`);

    const check = hairlineCheck(shadow);

    assert.equal(check.hairlines, 1, `${selector} has one hairline`);
    assert.deepEqual(check.problems, [], `${selector}'s hairline is ${HAIRLINE}`);
  }

  for (const { selector, name, value } of filterDeclarations.filter((entry) => entry.name === 'box-shadow' && /data-filter-dock/.test(entry.selector))) {
    assert.deepEqual(hairlineCheck(value).problems, [], `${selector} { ${name}: ${value} }`);
    assert.deepEqual(foreignShadows(value), [], `${selector} { ${name}: ${value} } casts a shadow in a colour other than the wall shade`);
  }

  const flyOver = dockValue('html[data-filter-dock="over"] .filter-panel', ['box-shadow']);

  assert.equal(shadowLayers(flyOver).length, 2, 'the fly-over has the hairline and one soft shadow');
});

test('the hairline and shade checks recognise a literal, a wrong share and a foreign token', () => {
  assert.deepEqual(hairlineCheck('inset -1px 0 0 color-mix(in srgb, var(--room-line, var(--chrome-line)) 40%, transparent)'), { hairlines: 1, problems: [] });
  assert.equal(hairlineCheck('inset -1px 0 0 #444').problems.length, 1);
  assert.equal(hairlineCheck('inset -1px 0 0 color-mix(in srgb, var(--room-line) 60%, transparent)').problems.length, 1);
  assert.equal(hairlineCheck('inset -1px 0 0 color-mix(in srgb, var(--room-text) 40%, transparent)').problems.length, 1);
  assert.equal(hairlineCheck('0 0 4px rgb(var(--room-shade-rgb) / 0.5)').hairlines, 0);
  assert.deepEqual(foreignShadows('12px 0 32px -12px rgb(var(--room-shade-rgb, 20 10 4) / 0.7)'), []);
  assert.equal(foreignShadows('12px 0 32px -12px rgb(0 0 0 / 0.7)').length, 1);
  assert.equal(foreignShadows('12px 0 32px -12px rgb(var(--wood-shade-rgb) / 0.7)').length, 1);
  assert.equal(foreignShadows('12px 0 32px -12px black').length, 1);
});

test('the dock draws its colours from the wall pairs the contrast test measures', () => {
  const filled = 'html[data-filter-dock] .filter-rail-button[data-filled]:not([data-unavailable])';
  const off = 'html[data-filter-dock] .filter-rail-button[data-unavailable]';

  assert.equal(dockValue(filled, ['background', 'background-color']), 'var(--room-text)', 'a filled rail button is filled with the text colour');
  assert.equal(dockValue(filled, ['color']), 'var(--room)', 'its symbol is the wall colour');
  assert.equal(dockValue(off, ['border']), '1px dashed var(--room-line)', 'a button that is off has the dashed line border');
  assert.equal(dockValue(off, ['color']), 'var(--room-muted)', 'and the muted symbol');
  assert.equal(dockValue('html[data-filter-dock] .filter-rail-count-number', ['color']), 'var(--room-text)');
  assert.equal(dockValue('html[data-filter-dock] .filter-rail-count-of', ['color']), 'var(--room-muted)');
  assert.equal(dockValue('html[data-filter-dock] .filter-rail-clear', ['border']), '1px solid var(--room-line)');
  assert.equal(dockValue('html[data-filter-dock] .filter-menu', ['border']), '1px solid var(--room-line)');
  assert.equal(dockValue('html[data-filter-dock] .filter-panel-title', ['color']), 'var(--room-text)');
  assert.equal(dockValue('html[data-filter-dock="over"] .filter-panel', ['background', 'background-color']), 'var(--room)', 'the fly-over stands on the flat wall colour');
  assert.equal(dockValue('html[data-filter-dock="push"] .filter-panel', ['background', 'background-color']), 'transparent', 'the pushed panel is the plain wall');
  assert.equal(dockValue('html[data-filter-dock] .filter-rail', ['background', 'background-color']), 'transparent', 'the rail is the plain wall');
});

test('no dock rule puts muted text on the rail hover wash or on a filled rail button', () => {
  const muted = (rule) => /--room-muted\b/.test(valueOf(rule, ['color']) ?? '');
  const washed = (rule) => /--room-(wash|text)\b/.test(withoutFallbacks(valueOf(rule, ['background', 'background-color']) ?? ''));

  for (const rule of dockRules.filter(washed)) {
    assert.equal(muted(rule), false, `${rule.selector} sets muted text on its own wash or fill`);

    for (const selector of selectorsOf(rule.selector).filter((entry) => /\.filter-rail-button/.test(entry))) {
      assert.match(selector, /:not\([^)]*\[data-unavailable\]/, `${selector} can wash or fill a button that is off, whose symbol is muted`);
    }
  }

  assert.ok(dockRules.filter(washed).length >= 2, 'the dock has its hover wash and its filled button');
});
