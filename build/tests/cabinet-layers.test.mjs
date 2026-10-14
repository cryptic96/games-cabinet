/**
 * Checks the shape of the cabinet's four ordered style layers with the plain Node test runner, from the style sheets read
 * as text. The layer order is declared once, first, in the site sheet; every cabinet rule lives inside its own layer,
 * because an unlayered rule would beat all of them; the wood layer holds colours and unitless strengths only; the design
 * layer writes no colour; and no selector combines two of the five look choices.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const LAYER_FILES = [
  ['cabinet.css', 'structure'],
  ['cabinet-design.css', 'design'],
  ['cabinet-wood.css', 'wood'],
  ['cabinet-modifiers.css', 'modifiers'],
];
const LOOK_ATTRIBUTES = ['design', 'wood', 'backs', 'lights', 'depth'];
const NEUTRAL_RGB = /^rgb\(\s*(0 0 0|255 255 255)\s*\/\s*[\d.]+%?\s*\)$/;
const NUMERIC_COLOUR = /\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(\s*[-\d.]/g;
const HEX_COLOUR = /#[0-9a-fA-F]{3,8}\b/g;
const LENGTH_UNIT = /(?<![\w.-])-?\d*\.?\d+(px|mm|rem|em|vh|vw|lvh|svh|dvh|cqw|cqh|cqi|cqb|cqmin|cqmax)\b/;
const WHOLE_PERCENT = /^-?\d*\.?\d+%$/;
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
  return withoutComments(readFileSync(new URL(`../../Cabinet.Service/wwwroot/css/${name}`, import.meta.url), 'utf8'));
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
 * Lists every rule of a block of style text with the selector it answers to and its declarations, looking inside media,
 * container and layer blocks.
 * @param {string} text The style text.
 * @returns {{ selector: string, declarations: { name: string, value: string }[] }[]}
 */
function rules(text) {
  const found = [];

  for (const item of items(text)) {
    if (item.body === null) {
      continue;
    }

    if (/^@(media|container|supports|layer)\b/.test(item.prelude)) {
      found.push(...rules(item.body));
    } else {
      found.push({ selector: item.prelude, declarations: declarations(item.body) });
    }
  }

  return found;
}

const sheets = Object.fromEntries(LAYER_FILES.map(([file, layer]) => [layer, readCss(file)]));
const allRules = Object.fromEntries(Object.entries(sheets).map(([layer, text]) => [layer, rules(text)]));
const site = readCss('site.css');

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
 * Finds the colour literals in a piece of style text: hexadecimal values, functions with numeric channels and named colours.
 * Words inside a variable name or a variable reference are not colours.
 * @param {string} text The style text.
 * @returns {string[]}
 */
function colourLiterals(text) {
  const cleaned = text.replace(/var\([^()]*(\([^()]*\)[^()]*)*\)/g, ' ').replace(/--[\w-]+/g, ' ');
  const literals = [...cleaned.matchAll(HEX_COLOUR)].map((match) => match[0]);

  for (const match of cleaned.matchAll(/\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(\s*[-\d.][^()]*\)/g)) {
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
 * Returns the custom property names declared at the root of the given sheet, wherever the rule sits.
 * @param {string} text The style sheet without comments.
 * @returns {Set<string>}
 */
function rootProperties(text) {
  const names = new Set();

  for (const rule of rules(text)) {
    if (selectorsOf(rule.selector).includes(':root')) {
      for (const declaration of rule.declarations) {
        names.add(declaration.name);
      }
    }
  }

  return names;
}

test('the layer order is the first rule of the first style sheet every page links', () => {
  assert.match(site.trimStart(), /^@layer structure, design, wood, modifiers;/);
});

test('every rule of a cabinet sheet lives inside the layer that sheet owns', () => {
  for (const [file, layer] of LAYER_FILES) {
    const top = items(sheets[layer]);

    assert.ok(top.length > 0, `${file} has rules`);

    for (const item of top) {
      assert.equal(item.body === null, false, `${file} has a statement outside its layer: ${item.prelude}`);
      assert.equal(item.prelude, `@layer ${layer}`, `${file} has a rule outside its layer: ${item.prelude}`);
    }
  }
});

test('the wood layer sets only custom properties, and none of their values is a length', () => {
  for (const rule of allRules.wood) {
    for (const { name, value } of rule.declarations) {
      assert.ok(name.startsWith('--'), `${rule.selector} sets ${name}`);
      assert.doesNotMatch(value, LENGTH_UNIT, `${name}: ${value}`);
      assert.doesNotMatch(value, WHOLE_PERCENT, `${name}: ${value}`);
    }
  }

  for (const forbidden of ['display', 'aspect-ratio', 'content-visibility', 'position', 'width', 'height']) {
    assert.equal(new RegExp(`(^|[;{\\s])${forbidden}\\s*:`).test(sheets.wood), false, forbidden);
  }
});

test('the design layer writes no colour literal', () => {
  assert.deepEqual(colourLiterals(sheets.design).filter((literal) => !['transparent', 'currentColor'].includes(literal)), []);
});

test('structure and modifiers write no colour literal other than neutral black and white with an alpha', () => {
  for (const layer of ['structure', 'modifiers']) {
    const literals = colourLiterals(sheets[layer]).filter((literal) => !['transparent', 'currentColor'].includes(literal));

    for (const literal of literals) {
      assert.match(literal, NEUTRAL_RGB, `${layer}: ${literal}`);
    }
  }
});

test('the colour checks recognise a literal when one is written', () => {
  assert.deepEqual(colourLiterals('color: #fff; background: rgb(1 2 3 / 0.5); border-color: red;').length, 3);
  assert.deepEqual(colourLiterals('color: rgb(var(--shade-rgb) / 0.2); background: var(--back, var(--wood));'), []);
});

test('the layer checks recognise a rule outside a layer and a selector that combines two choices', () => {
  const top = items('.a { color: red; } @layer wood { .b { --x: 1; } }');

  assert.deepEqual(top.map((item) => item.prelude), ['.a', '@layer wood']);
  assert.deepEqual(rules('@layer wood { @media (min-width: 1px) { .b, .c { --x: 1px; } } }').map((rule) => rule.selector), ['.b, .c']);
  assert.deepEqual(selectorsOf('[data-design="a"][data-wood="b"] .x, :is([data-depth="a"], [data-depth="b"]) .y'), [
    '[data-design="a"][data-wood="b"] .x',
    ':is([data-depth="a"], [data-depth="b"]) .y',
  ]);
});

test('no selector combines two of the five look choices, and none depends on the root element', () => {
  for (const [file, layer] of LAYER_FILES) {
    for (const rule of allRules[layer]) {
      for (const selector of selectorsOf(rule.selector)) {
        const used = LOOK_ATTRIBUTES.filter((name) => new RegExp(`\\[data-${name}\\b`).test(selector));

        assert.ok(used.length <= 1, `${file}: ${selector} combines ${used.join(' and ')}`);
        assert.doesNotMatch(selector, /:root\[data-/, `${file}: ${selector}`);
      }
    }
  }
});

test('the stacking map holds: light at 1, boxes and furniture at 2, the shade at 3, focus at 4, the floor behind', () => {
  const expected = new Map([
    ['.section > .section-light', '1'],
    ['.placement', '2'],
    ['.section > :is(.section-top, .section-trim, .section-base)', '2'],
    ['.cubby::after', '3'],
    ['.placement:focus-visible', '4'],
    ['.placement[data-open]', '4'],
    ['.section > .section-floor', '-1'],
  ]);
  const found = new Map();

  for (const [file, layer] of LAYER_FILES) {
    for (const rule of allRules[layer]) {
      for (const { name, value } of rule.declarations) {
        if (name === 'z-index') {
          assert.equal(found.has(rule.selector), false, `${file}: ${rule.selector} sets z-index twice`);
          found.set(rule.selector, value);
        }
      }
    }
  }

  assert.deepEqual([...found].sort(), [...expected].sort());
});

test('a cubby and a section body create no stacking context, so the light can slot between the back and the boxes', () => {
  const forbidden = ['z-index', 'opacity', 'transform', 'contain', 'isolation', 'filter', 'will-change', 'mix-blend-mode', 'perspective'];

  for (const [file, layer] of LAYER_FILES) {
    for (const rule of allRules[layer]) {
      for (const selector of selectorsOf(rule.selector)) {
        if (/(^|[\s>])\.(cubby|section-body)(\[[^\]]*\])*$/.test(selector)) {
          for (const { name } of rule.declarations) {
            assert.equal(forbidden.includes(name), false, `${file}: ${selector} sets ${name}`);
          }
        }
      }
    }
  }
});

test('every colour channel token a layer uses is declared by the wood layer', () => {
  const declared = new Set(allRules.wood.flatMap((rule) => rule.declarations.map((declaration) => declaration.name)));
  const used = new Set();

  for (const [file, layer] of LAYER_FILES) {
    for (const match of sheets[layer].matchAll(/var\(\s*(--[\w-]+-rgb)\b/g)) {
      used.add(match[1]);
      assert.ok(declared.has(match[1]), `${file} uses ${match[1]}, which the wood layer does not declare`);
    }
  }

  assert.ok(used.size > 0);
});

test('no wood property shares a name with the root of an unlayered sheet, and none is the renderer\'s frame width', () => {
  const unlayered = new Set(['site.css', 'card.css', 'owner.css'].flatMap((file) => [...rootProperties(readCss(file))]));
  const wood = new Set(allRules.wood.flatMap((rule) => rule.declarations.map((declaration) => declaration.name)));

  assert.ok(unlayered.size > 0);
  assert.ok(wood.size > 0);
  assert.equal(wood.has('--frame'), false);

  for (const name of wood) {
    assert.equal(unlayered.has(name), false, `${name} is declared on the root of an unlayered sheet`);
  }
});

test('every hairline the design layer draws in millimetres is at least one pixel', () => {
  const hairlines = allRules.design.flatMap((rule) => rule.declarations).filter(({ name }) => /^--(lw|groove|hair)$/.test(name));

  assert.ok(hairlines.length >= 2);

  for (const { name, value } of hairlines) {
    assert.match(value, /^max\(1px, /, `${name}: ${value}`);
  }
});

test('the chrome focus ring leaves the boxes to their own ring, and public chrome reads the room tokens', () => {
  assert.match(site, /button(:not\(\.placement\)|:where\(:not\(\.placement\)\)):focus-visible/);
  assert.doesNotMatch(site, /button:focus-visible/);

  const tokens = ['--wall-text', '--chrome-line', '--chrome-muted', '--wood-dark'];
  const fallback = new Map([
    ['--wall-text', '--room-text'],
    ['--chrome-line', '--room-line'],
    ['--chrome-muted', '--room-muted'],
    ['--wood-dark', '--room'],
  ]);
  const body = site.slice(site.indexOf('body {'));

  for (const token of tokens) {
    for (const match of body.matchAll(new RegExp(`var\\(${token}\\)`, 'g'))) {
      const before = body.slice(0, match.index);

      assert.ok(before.endsWith(`var(${fallback.get(token)}, `), `${token} is read without ${fallback.get(token)}`);
    }
  }
});

test('the section light is hidden outright until a look turns it on, and sits at the first level', () => {
  const light = allRules.structure.find((rule) => rule.selector === '.section > .section-light');

  assert.ok(light, 'the structure layer positions the light layer');
  assert.equal(light.declarations.find(({ name }) => name === 'display')?.value, 'none');
  assert.equal(light.declarations.find(({ name }) => name === 'opacity')?.value, '0');
});

/**
 * Reads the value a rule of the modifiers layer gives a property.
 * @param {string} selector The exact selector of the rule.
 * @param {string} property The property name.
 * @returns {string | undefined}
 */
function modifierValue(selector, property) {
  return allRules.modifiers
    .filter((rule) => rule.selector === selector)
    .flatMap((rule) => rule.declarations)
    .find(({ name }) => name === property)?.value;
}

test('the shade over the boxes is 0 when flat, 0.20 when shaded and 0.12 when deep, and never above the palette cap', () => {
  const palette = readFileSync(new URL('../../Cabinet.Domain/Layout/SpinePalette.cs', import.meta.url), 'utf8');
  const cap = Number(/MaxShadePercent\s*=\s*(\d+)/.exec(palette)?.[1]) / 100;

  assert.equal(cap, 0.2);
  assert.equal(modifierValue('[data-depth="flat"] .cabinet', '--shade-alpha'), '0');
  assert.equal(modifierValue('.cabinet', '--shade-alpha'), '0.20');
  assert.equal(modifierValue('[data-depth="deep"] .cabinet', '--shade-alpha'), '0.12');

  for (const rule of allRules.modifiers) {
    for (const { name, value } of rule.declarations) {
      if (name === '--shade-alpha') {
        assert.ok(Number(value) <= cap, `${rule.selector}: ${value}`);
      }
    }
  }
});

test('flat switches the depth strength off and painted backs switch the seams off and paint the back', () => {
  assert.equal(modifierValue(':root', '--depth-k'), '1');
  assert.equal(modifierValue('[data-depth="flat"]', '--depth-k'), '0');
  assert.equal(modifierValue(':root', '--seam-k'), '1');
  assert.equal(modifierValue('[data-backs="painted"]', '--seam-k'), '0');
  assert.equal(modifierValue('[data-backs="painted"]', '--back'), 'var(--back-painted)');
  assert.equal(modifierValue('[data-backs="painted"]', '--back-shade-k'), '1');
  assert.ok(sheets.modifiers.indexOf('[data-depth="flat"] {') > sheets.modifiers.indexOf(':root {'), 'flat comes after the defaults it replaces');
  assert.ok(sheets.modifiers.indexOf('[data-backs="painted"] {') > sheets.modifiers.indexOf(':root {'), 'painted comes after the defaults it replaces');
});

test('deep is four gradient layers on the cubby and one on each shelf board, with no clip and no extra element', () => {
  const gradients = (value) => (value.match(/(?<![\w-])linear-gradient\(/g) ?? []).length;
  const listLength = (value) => value.split(/,(?![^(]*\))/).length;
  const walls = modifierValue('[data-depth="deep"] .cubby', '--deep-img');
  const shelf = modifierValue('[data-depth="deep"] .cubby::before', '--top-face-img');

  assert.equal(gradients(walls), 4);
  assert.equal(gradients(shelf), 1);
  assert.equal(listLength(modifierValue('[data-depth="deep"] .cubby', '--deep-size')), 4);
  assert.equal(listLength(modifierValue('[data-depth="deep"] .cubby', '--deep-pos')), 4);
  assert.equal(/clip-path|cubby-deco/.test(sheets.modifiers), false);
});

test('every design rule that writes the background of a cubby or its shelf board keeps the deep slot as the first layer', () => {
  const first = (value) => value.split(/,(?![^(]*\))/)[0].trim();
  const slots = [
    { member: /\.cubby(\[[^\]]*\])?$/, image: 'var(--deep-img)', size: 'var(--deep-size)', position: 'var(--deep-pos)' },
    { member: /\.cubby(\[[^\]]*\])?::before$/, image: 'var(--top-face-img)', size: 'var(--top-face-size)', position: '0 0' },
  ];
  let checked = 0;

  for (const rule of allRules.design) {
    const value = (name) => rule.declarations.find((declaration) => declaration.name === name)?.value;
    const image = value('background-image');

    if (image === undefined) {
      continue;
    }

    for (const slot of slots.filter(({ member }) => selectorsOf(rule.selector).some((selector) => member.test(selector)))) {
      assert.equal(first(image), slot.image, `${rule.selector}: background-image`);
      assert.equal(first(value('background-size') ?? ''), slot.size, `${rule.selector}: background-size`);
      assert.equal(first(value('background-position') ?? ''), slot.position, `${rule.selector}: background-position`);
      checked += 1;
    }
  }

  assert.ok(checked >= 3, `only ${checked} cubby and shelf board backgrounds were found`);
});

test('the modifiers layer sets no size, position, margin, padding or display outside the light layer', () => {
  const forbidden = ['width', 'height', 'inset', 'top', 'left', 'right', 'bottom', 'aspect-ratio', 'margin', 'padding', 'display'];
  const light = /section-light|light-pool/;

  for (const rule of allRules.modifiers) {
    if (light.test(rule.selector)) {
      continue;
    }

    for (const { name } of rule.declarations) {
      assert.equal(forbidden.includes(name), false, `${rule.selector} sets ${name}`);
    }
  }
});

test('the depth and back rules are single-choice selectors on any ancestor, so combinations come from the cascade', () => {
  const choices = allRules.modifiers.flatMap((rule) => selectorsOf(rule.selector)).filter((selector) => /\[data-(depth|backs)\b/.test(selector));

  assert.ok(choices.length >= 5);

  for (const selector of choices) {
    assert.equal(LOOK_ATTRIBUTES.filter((name) => new RegExp(`\\[data-${name}\\b`).test(selector)).length, 1, selector);
  }
});
