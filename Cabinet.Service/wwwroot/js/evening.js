/**
 * Head script of the cabinet page. It runs before the first paint, only when the stored lights choice is not off, and does two
 * things. It marks the page as evening (data-evening) from the visitor's own clock, and when this browser tab has already faded
 * the lights on once and the lights are lit, it lights the page at once so a later load in the tab starts lit instead of fading
 * on again. The evening is the stretch from 30 minutes before sunset until sunrise, read from one fixed table of standard-time
 * sunrise and lamp-on minutes for a mid-European reference point, plus 60 minutes when the visitor's own clock is on summer
 * time. No location is read, stored or sent. The only name the script adds to the page is window.cabinetEvening.
 */
(function lightWhenItIsDark() {
  'use strict';

  const TAB_FLAG_KEY = 'cabinet-lit';
  const TAB_FLAG_VALUE = '1';
  const MINUTES_PER_HOUR = 60;
  const MILLISECONDS_PER_DAY = 86400000;
  const DAYS_PER_YEAR = 365;
  const NOON = 12;
  const root = document.documentElement;

  /**
   * Standard-time minutes since midnight at 52 degrees north, 5 degrees east, on the 1st, 11th and 21st of every month:
   * [day of the year, sunrise, lamp on]. Lamp on is 30 minutes before sunset. Rounded to whole minutes.
   */
  const TABLE = [
    [1, 528, 969],
    [11, 524, 981],
    [21, 515, 997],
    [32, 500, 1017],
    [42, 483, 1036],
    [52, 463, 1054],
    [60, 446, 1069],
    [70, 424, 1086],
    [80, 401, 1104],
    [91, 375, 1123],
    [101, 352, 1140],
    [111, 331, 1157],
    [121, 310, 1174],
    [131, 293, 1190],
    [141, 278, 1205],
    [152, 266, 1220],
    [162, 260, 1229],
    [172, 260, 1234],
    [182, 264, 1233],
    [192, 273, 1228],
    [202, 286, 1217],
    [213, 302, 1201],
    [223, 318, 1183],
    [233, 334, 1163],
    [244, 352, 1139],
    [254, 368, 1116],
    [264, 384, 1092],
    [274, 401, 1069],
    [284, 418, 1046],
    [294, 435, 1024],
    [305, 455, 1003],
    [315, 472, 985],
    [325, 490, 972],
    [335, 505, 962],
    [345, 518, 958],
    [355, 526, 960],
  ];

  /**
   * Counts the day of the year of a date on the visitor's calendar, January 1 being day 1.
   * @param {Date} date The date to count.
   * @returns {number}
   */
  function dayOfYear(date) {
    const start = Date.UTC(date.getFullYear(), 0, 1);
    const today = Date.UTC(date.getFullYear(), date.getMonth(), date.getDate());

    return Math.round((today - start) / MILLISECONDS_PER_DAY) + 1;
  }

  /**
   * Reads the table for a day of the year by linear interpolation between the two neighbouring rows; after the last row it
   * runs on towards the first row of the next year, so the year's end has no jump.
   * @param {number} day The day of the year.
   * @returns {{ rise: number, lampOn: number }} Standard-time minutes since midnight, whole minutes.
   */
  function standardMinutes(day) {
    let index = 0;

    while (index < TABLE.length - 1 && TABLE[index + 1][0] <= day) {
      index += 1;
    }

    const lower = TABLE[index];
    const upper = index + 1 < TABLE.length ? TABLE[index + 1] : [TABLE[0][0] + DAYS_PER_YEAR, TABLE[0][1], TABLE[0][2]];
    const share = (day - lower[0]) / (upper[0] - lower[0]);

    return {
      rise: Math.round(lower[1] + (share * (upper[1] - lower[1]))),
      lampOn: Math.round(lower[2] + (share * (upper[2] - lower[2]))),
    };
  }

  /**
   * Tells whether the visitor's clock is on summer time: its offset from universal time is smaller than the larger of the
   * January and July offsets of the same year. A zone without summer time is never on it.
   * @param {Date} date The moment to ask about.
   * @returns {boolean}
   */
  function summerTime(date) {
    const january = new Date(date.getFullYear(), 0, 1).getTimezoneOffset();
    const july = new Date(date.getFullYear(), 6, 1).getTimezoneOffset();

    return date.getTimezoneOffset() < Math.max(january, july);
  }

  /**
   * Gives the sunrise and lamp-on minutes of a day on the visitor's own clock.
   * @param {Date} date Any moment of that day.
   * @returns {{ rise: number, lampOn: number }} Minutes since the visitor's midnight.
   */
  function clockMinutes(date) {
    const minutes = standardMinutes(dayOfYear(date));
    const shift = summerTime(date) ? MINUTES_PER_HOUR : 0;

    return { rise: minutes.rise + shift, lampOn: minutes.lampOn + shift };
  }

  /**
   * Tells whether it is evening: at or after the lamp-on minute of the day, or before its sunrise.
   * @param {Date} now The moment to ask about.
   * @returns {boolean}
   */
  function isEvening(now) {
    const times = clockMinutes(new Date(now.getFullYear(), now.getMonth(), now.getDate(), NOON));
    const seconds = now.getSeconds() + (now.getMilliseconds() / 1000);
    const minutes = (now.getHours() * MINUTES_PER_HOUR) + now.getMinutes() + (seconds / MINUTES_PER_HOUR);

    return minutes >= times.lampOn || minutes < times.rise;
  }

  /**
   * Gives the time until the next sunrise or lamp-on moment on the visitor's clock.
   * @param {Date} now The moment to count from.
   * @returns {number} Milliseconds, more than zero.
   */
  function msToNextChange(now) {
    const nowTime = now.getTime();
    let next = Infinity;

    for (const dayOffset of [0, 1]) {
      const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() + dayOffset, NOON);
      const times = clockMinutes(day);

      for (const minutes of [times.rise, times.lampOn]) {
        const moment = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, minutes).getTime();

        if (moment > nowTime && moment < next) {
          next = moment;
        }
      }
    }

    return next - nowTime;
  }

  /**
   * Tells whether the attributes of an element say the lights are lit: always on, or evenings while it is evening.
   * @param {Element} element The element that carries the look attributes.
   * @returns {boolean}
   */
  function litByAttributes(element) {
    const lights = element.getAttribute('data-lights');

    return lights === 'on' || (lights === 'evenings' && element.hasAttribute('data-evening'));
  }

  /**
   * Marks an element lit when its attributes say the lights are lit.
   * @param {Element} element The element that carries the look attributes.
   * @returns {boolean} Whether the element is lit.
   */
  function markLit(element) {
    const lit = litByAttributes(element);

    if (lit) {
      element.setAttribute('data-lit', '');
    }

    return lit;
  }

  /**
   * Tells whether this tab has already faded the lights on. Storage that is not available counts as not.
   * @returns {boolean}
   */
  function tabAlreadyFadedIn() {
    try {
      return window.sessionStorage.getItem(TAB_FLAG_KEY) === TAB_FLAG_VALUE;
    } catch {
      return false;
    }
  }

  window.cabinetEvening = { key: TAB_FLAG_KEY, markLit, isEvening, msToNextChange };

  if (root.getAttribute('data-lights') !== 'off') {
    if (isEvening(new Date())) {
      root.setAttribute('data-evening', '');
    }

    if (tabAlreadyFadedIn()) {
      markLit(root);
    }
  }
}());
