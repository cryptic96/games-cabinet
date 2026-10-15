/**
 * Checks the evening rule of the head script with the plain Node test runner: when it is evening by the visitor's own clock,
 * how summer time moves it, what the script marks before the first paint, and when the next change is due. The script runs in an
 * isolated context against a fake page and a fake clock, so the result does not depend on the zone of the machine.
 *
 * The table's own values are checked against an independent solar calculation in the unit tests; here only a few interpolated
 * values are needed, read from the rows next to the day: in standard time the sunrise and the lamp-on minute are 520 and 987 on
 * 15 January, 278 and 1224 on 15 July, and 980 for the lamp-on minute on 15 November.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { fakeDate, fakeRoot, fakeStorage } from './fake-page.mjs';

const EVENING_SOURCE = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/evening.js', import.meta.url), 'utf8');
const MINUTE = 60000;
const JANUARY = 0;
const JULY = 6;
const NOVEMBER = 10;
const DECEMBER = 11;
const WINTER_OFFSET = -60;
const SUMMER_OFFSET = -120;
const JANUARY_15_RISE = 520;
const JANUARY_15_LAMP_ON = 987;
const JULY_15_RISE = 278;
const JULY_15_LAMP_ON = 1224;
const NOVEMBER_15_LAMP_ON = 980;

/**
 * Runs the head script against a fake page and a fake clock and hands back what it exposed.
 * @param {object} setup The look and markers on the root, the storage, and the clock.
 * @returns {{ root: object, bridge: object, now: Date, clockClass: typeof Date }} The root, the bridge and the clock.
 */
function runScript({ dataset = { lights: 'evenings' }, attributes = [], storage = fakeStorage(), local, winter = WINTER_OFFSET, summer = SUMMER_OFFSET }) {
  const root = fakeRoot(dataset, attributes);
  const clockClass = fakeDate({ reading: local, winter, summer });
  const window = {};

  Object.defineProperty(window, 'sessionStorage', { get: () => storage, enumerable: true });
  vm.runInContext(EVENING_SOURCE, vm.createContext({ window, document: { documentElement: root }, Date: clockClass }));

  return { root, bridge: window.cabinetEvening, now: new clockClass(), clockClass };
}

/**
 * Asks the script whether it is evening at a local clock reading.
 * @param {number[]} local Year, month (from 0), day, hour, minute and optionally second.
 * @param {object} [zone] The winter and summer offsets of the zone.
 * @returns {boolean}
 */
function eveningAt(local, zone = {}) {
  const { bridge, clockClass } = runScript({ local, ...zone });

  return bridge.isEvening(new clockClass(...local));
}

/**
 * Finds the first minute of the afternoon that is evening on a day, which is that day's lamp-on minute on the visitor's clock.
 * @param {number[]} day Year, month (from 0) and day.
 * @param {object} [zone] The winter and summer offsets of the zone.
 * @returns {number} Minutes since midnight.
 */
function lampOnMinute(day, zone = {}) {
  for (let minute = 12 * 60; minute < 24 * 60; minute += 1) {
    if (eveningAt([...day, Math.floor(minute / 60), minute % 60], zone)) {
      return minute;
    }
  }

  return Number.NaN;
}

test('on a winter day the lamp-on minute is evening, one minute before it is not, and the sunrise minute is not, one before it is', () => {
  const lampOn = [2027, JANUARY, 15, Math.floor(JANUARY_15_LAMP_ON / 60), JANUARY_15_LAMP_ON % 60];
  const rise = [2027, JANUARY, 15, Math.floor(JANUARY_15_RISE / 60), JANUARY_15_RISE % 60];

  assert.equal(eveningAt(lampOn), true, 'lamp-on itself');
  assert.equal(eveningAt([...lampOn.slice(0, 4), lampOn[4] - 1, 59]), false, 'a second before lamp-on');
  assert.equal(eveningAt(rise), false, 'sunrise itself');
  assert.equal(eveningAt([...rise.slice(0, 4), rise[4] - 1, 59]), true, 'a second before sunrise');
  assert.equal(eveningAt([2027, JANUARY, 15, 12, 0]), false, 'midday');
  assert.equal(eveningAt([2027, JANUARY, 15, 23, 59]), true, 'late');
  assert.equal(eveningAt([2027, JANUARY, 15, 0, 0]), true, 'midnight');
});

test('in summer time the visitor is on the clock an hour later: lamp-on and sunrise both move by 60 minutes', () => {
  assert.equal(lampOnMinute([2027, JULY, 15]), JULY_15_LAMP_ON + 60);
  assert.equal(eveningAt([2027, JULY, 15, 5, 37]), true, 'a minute before the summer sunrise');
  assert.equal(eveningAt([2027, JULY, 15, 5, 38]), false, 'the summer sunrise');
  assert.equal(eveningAt([2027, JULY, 15, 21, 23]), false, 'a minute before the summer lamp-on');
  assert.equal(eveningAt([2027, JULY, 15, 21, 24]), true, 'the summer lamp-on');
});

test('the summer-time flag follows the visitor: on only while the current offset is under the larger of the January and July offsets', () => {
  assert.equal(lampOnMinute([2027, JULY, 15]), JULY_15_LAMP_ON + 60, 'July is in summer time');
  assert.equal(lampOnMinute([2027, JANUARY, 15]), JANUARY_15_LAMP_ON, 'January is not');
  assert.equal(lampOnMinute([2027, NOVEMBER, 15]), NOVEMBER_15_LAMP_ON, 'November is not');
});

test('a zone without summer time is never shifted, in July as in January', () => {
  const zone = { winter: -60, summer: -60 };

  assert.equal(lampOnMinute([2027, JULY, 15], zone), JULY_15_LAMP_ON);
  assert.equal(eveningAt([2027, JULY, 15, 4, 37], zone), true);
  assert.equal(eveningAt([2027, JULY, 15, 4, 38], zone), false);
  assert.equal(lampOnMinute([2027, JANUARY, 15], zone), JANUARY_15_LAMP_ON);
});

test('a zone in the southern hemisphere, whose summer time falls in January, is shifted in January and not in July', () => {
  const zone = { winter: -660, summer: -600 };

  assert.equal(lampOnMinute([2027, JANUARY, 15], zone), JANUARY_15_LAMP_ON + 60);
  assert.equal(lampOnMinute([2027, JULY, 15], zone), JULY_15_LAMP_ON);
});

test('the lamp-on minute runs on from the last day of December into January without a jump', () => {
  const lastDay = lampOnMinute([2027, DECEMBER, 31]);
  const nextFirst = lampOnMinute([2028, JANUARY, 1]);
  const leapLastDay = lampOnMinute([2028, DECEMBER, 31]);

  assert.ok(Math.abs(lastDay - nextFirst) <= 2, `${lastDay} then ${nextFirst}`);
  assert.ok(Math.abs(leapLastDay - nextFirst) <= 2, `${leapLastDay} then ${nextFirst}`);
  assert.ok(Math.abs(lampOnMinute([2027, DECEMBER, 30]) - lastDay) <= 2);
});

test('before the first paint the head script marks the evening whenever the lights are not off', () => {
  const night = [2027, JANUARY, 15, 20, 0];
  const day = [2027, JANUARY, 15, 12, 0];

  assert.equal(runScript({ dataset: { lights: 'evenings' }, local: night }).root.hasAttribute('data-evening'), true);
  assert.equal(runScript({ dataset: { lights: 'on' }, local: night }).root.hasAttribute('data-evening'), true);
  assert.equal(runScript({ dataset: { lights: 'evenings' }, local: day }).root.hasAttribute('data-evening'), false);
  assert.equal(runScript({ dataset: { lights: 'off' }, local: night }).root.present.size, 0, 'with the lights off nothing is marked');
});

test('with the tab flag set, evenings only is lit before the first paint at night and not by day', () => {
  const flagged = () => fakeStorage({ initial: { 'cabinet-lit': '1' } });
  const night = runScript({ local: [2027, JANUARY, 15, 20, 0], storage: flagged() });
  const day = runScript({ local: [2027, JANUARY, 15, 12, 0], storage: flagged() });
  const unflagged = runScript({ local: [2027, JANUARY, 15, 20, 0] });

  assert.equal(night.root.hasAttribute('data-evening'), true);
  assert.equal(night.root.hasAttribute('data-lit'), true);
  assert.equal(day.root.hasAttribute('data-lit'), false);
  assert.equal(unflagged.root.hasAttribute('data-evening'), true);
  assert.equal(unflagged.root.hasAttribute('data-lit'), false, 'the first load in a tab is lit by the module after the first paint');
});

test('with the lights on and the tab flag set, the script lights the page; with storage that throws it does not', () => {
  const lit = runScript({ dataset: { lights: 'on' }, local: [2027, JANUARY, 15, 12, 0], storage: fakeStorage({ initial: { 'cabinet-lit': '1' } }) });
  const refused = runScript({ dataset: { lights: 'on' }, local: [2027, JANUARY, 15, 12, 0], storage: fakeStorage({ throws: true }) });

  assert.equal(lit.root.hasAttribute('data-lit'), true);
  assert.equal(refused.root.hasAttribute('data-lit'), false);
});

test('the next change is the lamp-on minute by day, and the next sunrise at night, including after midnight', () => {
  const noon = runScript({ local: [2027, JANUARY, 15, 12, 0] });
  const evening = runScript({ local: [2027, JANUARY, 15, 20, 0] });
  const small = runScript({ local: [2027, JANUARY, 15, 3, 0] });

  assert.equal(noon.bridge.msToNextChange(noon.now), (JANUARY_15_LAMP_ON - (12 * 60)) * MINUTE);
  assert.equal(evening.bridge.msToNextChange(evening.now), (((24 * 60) - (20 * 60)) + JANUARY_15_RISE) * MINUTE);
  assert.equal(small.bridge.msToNextChange(small.now), (JANUARY_15_RISE - (3 * 60)) * MINUTE);
});

test('the next change in summer time is the shifted lamp-on minute', () => {
  const noon = runScript({ local: [2027, JULY, 15, 12, 0] });

  assert.equal(noon.bridge.msToNextChange(noon.now), (JULY_15_LAMP_ON + 60 - (12 * 60)) * MINUTE);
});

test('at the exact moment of a change the next change is the one after it', () => {
  const atLampOn = runScript({ local: [2027, JANUARY, 15, Math.floor(JANUARY_15_LAMP_ON / 60), JANUARY_15_LAMP_ON % 60] });

  assert.ok(atLampOn.bridge.msToNextChange(atLampOn.now) > 0);
  assert.equal(atLampOn.bridge.msToNextChange(atLampOn.now), (((24 * 60) - JANUARY_15_LAMP_ON) + JANUARY_15_RISE) * MINUTE);
});
