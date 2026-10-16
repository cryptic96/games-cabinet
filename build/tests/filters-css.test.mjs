/**
 * Checks the structure rules of the filter style sheet with the plain Node test runner: the sheet is read from disk as text and its
 * rules are looked at one by one. These are the promises a later edit must not break: the sheet writes no colour of its own, uses no
 * blur and no filter, never reaches into the cabinet's boxes, moves things only with opacity and transform, keeps the bar at its level,
 * never sets muted text on a wash or a chosen fill, and leaves the shadow of a dimmed box to the modifiers layer.
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

test('a state selector is matched to the element it styles', () => {
  assert.equal(baseSelector('.filter-chip:hover'), '.filter-chip');
  assert.equal(baseSelector('.filter-chip:has(input:checked:not([value=""]))'), '.filter-chip');
  assert.equal(baseSelector('.filter-view-segment:has(input:checked):hover'), '.filter-view-segment');
  assert.equal(withoutFallbacks('var(--room-wash, color-mix(in srgb, var(--room-text, red) 8%, transparent))'), 'var(--room-wash)');
});
