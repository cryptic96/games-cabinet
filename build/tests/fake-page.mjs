/**
 * Fakes of the page, of the browser storage and of the clock that the script tests hand to the cabinet's scripts, so what a script
 * may touch is exactly what the test gives it.
 */

const FIRST_SUMMER_MONTH = 3;
const LAST_SUMMER_MONTH = 9;

/**
 * Builds a fake root element that keeps its attributes and its listeners.
 * @param {object} dataset The data attributes the server wrote.
 * @param {string[]} [attributes] Attributes that are present before any script runs.
 * @returns {object} The root.
 */
export function fakeRoot(dataset, attributes = []) {
  const present = new Set(attributes);
  const listeners = new Set();

  return {
    dataset,
    present,
    listeners,
    hasAttribute: (name) => present.has(name),
    setAttribute: (name) => present.add(name),
    removeAttribute: (name) => present.delete(name),
    getAttribute: (name) => (present.has(name) ? '' : (dataset[name.replace(/^data-/, '')] ?? null)),
    addEventListener: (type, handler) => listeners.add(handler),
    removeEventListener: (type, handler) => listeners.delete(handler),
  };
}

/**
 * Builds a fake storage that holds what is written to it, or throws on every use.
 * @param {object} [options] Whether the storage refuses every use, and what it holds before the script runs.
 * @returns {object} The storage.
 */
export function fakeStorage({ throws = false, initial = {} } = {}) {
  const values = new Map(Object.entries(initial));

  return {
    values,
    getItem: (key) => {
      if (throws) {
        throw new Error('storage refused');
      }

      return values.has(key) ? values.get(key) : null;
    },
    setItem: (key, value) => {
      if (throws) {
        throw new Error('storage refused');
      }

      values.set(key, value);
    },
  };
}

/**
 * Builds a Date class for a script that runs in an isolated context. Its local clock is the given reading whatever the zone of
 * the machine that runs the test: the local parts of a date are read as the parts of universal time, and the offset from
 * universal time is the winter offset from November to March and the summer offset from April to October, as a zone with
 * summer time reports it.
 * @param {object} setup The reading of the local clock and the offsets.
 * @param {number[]} setup.reading The local year, month (counted from 0), day, hour and minute, and optionally second.
 * @param {number} [setup.winter] The offset in minutes outside summer time, as getTimezoneOffset reports it.
 * @param {number} [setup.summer] The offset in minutes in summer time; the winter offset by default, a zone without summer time.
 * @returns {typeof Date} The class.
 */
export function fakeDate({ reading, winter = -60, summer = winter }) {
  const fixed = Date.UTC(...reading);

  return class FakeDate extends Date {
    constructor(...parts) {
      if (parts.length === 0) {
        super(fixed);
      } else if (parts.length === 1) {
        super(parts[0]);
      } else {
        super(Date.UTC(...parts));
      }
    }

    getFullYear() {
      return this.getUTCFullYear();
    }

    getMonth() {
      return this.getUTCMonth();
    }

    getDate() {
      return this.getUTCDate();
    }

    getHours() {
      return this.getUTCHours();
    }

    getMinutes() {
      return this.getUTCMinutes();
    }

    getSeconds() {
      return this.getUTCSeconds();
    }

    getMilliseconds() {
      return this.getUTCMilliseconds();
    }

    getTimezoneOffset() {
      const month = this.getUTCMonth();

      return month >= FIRST_SUMMER_MONTH && month <= LAST_SUMMER_MONTH ? summer : winter;
    }
  };
}
