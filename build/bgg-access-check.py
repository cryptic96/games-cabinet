#!/usr/bin/env python3
"""Shape-only access check for the BoardGameGeek XML API.

The cabinet reads the owner's collection from BoardGameGeek with an
application token. Several details of that conversation can only be learned
by asking the real service with the real token: whether the private
inventory location is returned, how queued answers behave, which fields and
units the collection carries, and whether a User-Agent is required.

This script asks those questions once, politely, and prints only the shape
of the answers: call labels, status codes, a few whitelisted response
header names and values, body classes, counts, element and attribute names
and numeric ranges. It never prints a title, a location value, a URL, the
token, the username or any markup. Before anything is printed the whole
report is scanned for those values, and if any is found nothing is printed
at all.

Two suites exist. The access suite (the default) asks about access: private
fields, queued answers, units and the User-Agent rule. The art suite asks
where box images live, which details the per-game call carries and how the
image host behaves, and prints counts and classes only.

Modes:
  --plan       print the call list; reads nothing and uses no network
  --self-test  run the summariser and the output guard on synthetic data
  --run        ask BoardGameGeek; reads the token, username and optional
               contact address from the env file (default
               /etc/cabinet/cabinet.env), never from the command line
  --suite      access (default) or art; chooses the call list for --plan and
               --run, and with --self-test restricts the proof to one suite

Exit codes: 0 success, 2 usage, 3 not configured, 4 output withheld,
5 a stop rule fired (the shape gathered so far is still printed).

Only the Python standard library is used.
"""

import argparse
import collections
import http.client
import math
import re
import ssl
import statistics
import struct
import sys
import time
import typing
import xml.etree.ElementTree as ET
import xml.parsers.expat as expat
from urllib.parse import urlencode, urlsplit

HOST = "boardgamegeek.com"
API_PREFIX = "/xmlapi2/"
MAX_REQUESTS = 14
MIN_GAP_SECONDS = 6.0
POLL_WAITS = (5, 10, 20, 30, 30, 30)
MAX_BODY_BYTES = 20 * 1024 * 1024
REQUEST_TIMEOUT_SECONDS = 60
WRONG_TOKEN = "invalid-token-for-shape-check"
MIN_SUBSTRING_NAME_LENGTH = 4
USER_AGENT = "GamesCabinet-access-check/1"
DEFAULT_ENV_FILE = "/etc/cabinet/cabinet.env"
SHOWN_HEADERS = ("content-type", "content-length", "cache-control", "retry-after", "server", "cf-mitigated")
STOP_STATUSES = (401, 403, 429)
STOP_LABELS = "ABCDEFG"
MAX_CONSECUTIVE_TROUBLES = 2

IMAGE_HOSTS_KNOWN = ("cf.geekdo-images.com",)
IMAGE_HOST_SUFFIX = "geekdo-images.com"
IMAGE_GAP_SECONDS = 1.5
MAX_IMAGE_REQUESTS = 7
MAX_IMAGE_BYTES = 12 * 1024 * 1024
ART_MAX_API_REQUESTS = 10
PIXEL_CAP = 36_000_000
ART_STOP_LABELS = "JKL"
IMAGE_ACCEPT = "image/*"
MAX_ART_VERSION_IMAGES = 3
MAX_ART_ITEM_IMAGES = 3
MAX_THING_IDS_PER_KIND = 2
MIN_ID_DIGITS = 4
SUITES = ("access", "art")

EXIT_OK = 0
EXIT_USAGE = 2
EXIT_NOT_CONFIGURED = 3
EXIT_WITHHELD = 4
EXIT_STOPPED = 5

WITHHELD_MESSAGE = "output withheld: it would have contained a sensitive value"
WITHHELD_HINT = (
    "hint: the report matched the token, the contact address, the username (a name under four characters "
    "only as a whole word) or a title, location, image address or identifier seen in the answers"
)

USERNAME_PARAMETER = None


class Call(typing.NamedTuple):
    """One planned request: its label, endpoint, query parameters and headers."""

    label: str
    endpoint: str
    parameters: tuple
    auth: str
    user_agent: bool


class Result(typing.NamedTuple):
    """What one planned request produced, before it is summarised."""

    status: typing.Optional[int]
    seconds: int
    headers: list
    body: bytes
    answers_202: int
    error: typing.Optional[str]
    skipped: typing.Optional[str]
    budget_reached: bool


CALLS = (
    Call("A", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("excludesubtype", "boardgameexpansion"), ("stats", "1"), ("version", "1")), "token", True),
    Call("B", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("excludesubtype", "boardgameexpansion"), ("stats", "1"), ("version", "1"), ("showprivate", "1")), "token", True),
    Call("C", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("subtype", "boardgameexpansion"), ("stats", "1"), ("version", "1")), "token", True),
    Call("D", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("stats", "1")), "token", True),
    Call("E", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("showprivate", "1"), ("subtype", "boardgameexpansion")), "token", True),
    Call("F", "thing", (("id", "13"),), "token", True),
    Call("G", "thing", (("id", "13"),), "token", False),
    Call("H", "collection", (("username", USERNAME_PARAMETER), ("own", "1")), "none", True),
    Call("I", "collection", (("username", USERNAME_PARAMETER), ("own", "1")), "wrong", True),
)

COLLECTION_OPTIONAL_FIELDS = ("yearpublished", "numplays", "comment", "image", "thumbnail")
VERSION_DIMENSIONS = ("width", "length", "depth")

NAME_CHARACTERS = re.compile(r"[^A-Za-z0-9_.:\-]")
VALUE_CHARACTERS = re.compile(r"[^A-Za-z0-9 ,;=:./_+*()\-]")
TOKEN_VALUE = re.compile(r"[a-z0-9_]{1,30}")


def safe_name(text):
    """Reduce an element or attribute name to harmless characters and a short length."""
    return NAME_CHARACTERS.sub("_", text)[:40]


def safe_header_value(text):
    """Reduce a response header value to harmless characters, dropping anything URL-like."""
    if "http" in text.lower():
        return "omitted"
    return VALUE_CHARACTERS.sub("_", text)[:100]


def safe_category(value):
    """Keep a short lowercase category value as is; anything else becomes 'other'."""
    if value is None:
        return "missing"
    if TOKEN_VALUE.fullmatch(value):
        return value
    return "other"


def parse_env_file(path):
    """Read KEY=VALUE lines into a dict, skipping blanks and comment lines."""
    values = {}
    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            if line.startswith("export "):
                line = line[len("export "):].lstrip()
            key, _, value = line.partition("=")
            value = value.strip()
            if len(value) >= 2 and value[0] == value[-1] and value[0] in ("'", '"'):
                value = value[1:-1]
            values[key.strip()] = value
    return values


def is_header_safe(text):
    """True when a configured value is printable ASCII without spaces or controls."""
    return bool(text) and all(33 <= ord(character) <= 126 for character in text)


def load_configuration(path):
    """Return (token, username, contact) from the env file, or None when not configured."""
    try:
        values = parse_env_file(path)
    except OSError:
        return None
    token = values.get("Bgg__Token", "")
    username = values.get("Bgg__Username", "")
    contact = values.get("Bgg__ContactUrl", "")
    if not is_header_safe(token) or not username.strip():
        return None
    if contact and not is_header_safe(contact):
        contact = ""
    return token, username.strip(), contact


class Collector:
    """Remembers sensitive values and per-call facts that later calls are compared against."""

    def __init__(self):
        self.sensitive = set()
        self.numbers = set()
        self.keys = {}
        self.subtypes = {}
        self.counts = {}

    def note(self, value):
        """Remember a title or location value so the guard can look for it in the report."""
        text = (value or "").strip()
        if len(text) < 2:
            return
        if len(text) < 4 and text.isdigit():
            return
        self.sensitive.add(text)

    def note_number(self, value):
        """Remember an identifier so the guard can look for it as a whole run of digits."""
        text = (value or "").strip()
        if text.isdigit() and len(text) >= MIN_ID_DIGITS:
            self.numbers.add(text)


def contains_number(report, number):
    """True when the report holds the identifier as a whole run of digits."""
    pattern = r"(?<![0-9])" + re.escape(number) + r"(?![0-9])"
    return re.search(pattern, report) is not None


def contains_value(report, value):
    """Whole-word match for short values, substring match for longer ones."""
    if len(value) < 4:
        pattern = r"(?<![A-Za-z0-9])" + re.escape(value) + r"(?![A-Za-z0-9])"
        return re.search(pattern, report) is not None
    return value in report


def contains_name(report, name):
    """Case-insensitive match of a username: anywhere for a long one, as a whole word for a short one."""
    if not name:
        return False
    if len(name) < MIN_SUBSTRING_NAME_LENGTH:
        pattern = r"(?<![A-Za-z0-9])" + re.escape(name) + r"(?![A-Za-z0-9])"
        return re.search(pattern, report, re.IGNORECASE) is not None
    return name.lower() in report.lower()


def guarded_credentials(token, contact):
    """The secrets that withhold the report when they appear anywhere in it, even inside a longer word."""
    return [token, contact, WRONG_TOKEN]


def report_leaks(report, credentials, values, names=(), numbers=()):
    """True when the report holds a credential, a username, a remembered value or identifier, a URL marker or markup."""
    lowered = report.lower()
    if "http" in lowered or "<" in report:
        return True
    for credential in credentials:
        if credential and credential.lower() in lowered:
            return True
    if any(contains_name(report, name) for name in names):
        return True
    if any(contains_number(report, number) for number in numbers):
        return True
    return any(contains_value(report, value) for value in values)


WIDE_ENCODING_MARKS = (b"\xff\xfe", b"\xfe\xff", b"\x00\x00\xfe\xff")


class DoctypeRefused(Exception):
    """Raised by the parser callbacks when a document declares a doctype or an entity."""


def refuse_declaration(*arguments):
    """Parser callback: any doctype or entity declaration is refused outright."""
    raise DoctypeRefused()


def is_wide_encoded(body):
    """True for a body in a UTF-16 or UTF-32 form: it starts with their byte-order mark or holds a NUL byte."""
    return body.startswith(WIDE_ENCODING_MARKS) or b"\x00" in body


def declares_doctype_or_entity(body):
    """True when a strict parse meets a doctype or an entity declaration, or cannot parse the body at all."""
    parser = expat.ParserCreate()
    parser.StartDoctypeDeclHandler = refuse_declaration
    parser.EntityDeclHandler = refuse_declaration
    try:
        parser.Parse(body, True)
    except DoctypeRefused:
        return True
    except expat.ExpatError:
        return False
    return False


def classify_body(body):
    """Return (class, parsed root or None) for a response body."""
    if not body.strip():
        return "empty", None
    if len(body) > MAX_BODY_BYTES or is_wide_encoded(body):
        return "other", None
    lowered = body.lower()
    head = lowered[:2048].lstrip()
    if head.startswith((b"<!doctype html", b"<html")) or b"<html" in head:
        return "html", None
    if b"<!doctype" in lowered or b"<!entity" in lowered or declares_doctype_or_entity(body):
        return "other", None
    try:
        root = ET.fromstring(body)
    except ET.ParseError:
        return "other", None
    tag = root.tag
    if tag == "items":
        return "xml:items", root
    if tag == "errors":
        return "xml:errors", root
    if tag == "message":
        return "xml:message", root
    return "xml:other", root


def format_counter(title, counter):
    """One report line listing a counter's keys and counts."""
    listing = ", ".join("%s %d" % (key, counter[key]) for key in sorted(counter))
    return "  %s: %s" % (title, listing or "none")


def format_number(value):
    """Format a measurement with at most two decimals."""
    return ("%.2f" % value).rstrip("0").rstrip(".")


def describe_dimension(name, readings):
    """One report line for a version dimension from its collected readings."""
    forms = readings["forms"]
    numbers = readings["nonzero"]
    if numbers:
        spread = "min %s, median %s, max %s" % (
            format_number(min(numbers)),
            format_number(statistics.median(numbers)),
            format_number(max(numbers)),
        )
    else:
        spread = "min none, median none, max none"
    return "  %s: value attribute %d, text %d, non-zero %d, zero %d, unparseable %d, %s" % (
        name,
        forms["value attribute"],
        forms["text"],
        len(numbers),
        readings["zero"],
        readings["unparseable"],
        spread,
    )


def read_dimension(version, name):
    """Return (form, raw text) for a dimension element under a version, or None."""
    element = version.find(".//" + name)
    if element is None:
        return None
    raw = element.get("value")
    if raw is not None:
        return "value attribute", raw
    return "text", (element.text or "").strip()


def summarise_versions(items):
    """Report lines about the owner-selected version carried by each collection item."""
    readings = {
        name: {"forms": collections.Counter(), "nonzero": [], "zero": 0, "unparseable": 0}
        for name in VERSION_DIMENSIONS
    }
    with_version = 0
    length_not_smaller = 0
    for item in items:
        version = item.find("version")
        if version is None:
            continue
        with_version += 1
        numbers = {}
        for name in VERSION_DIMENSIONS:
            found = read_dimension(version, name)
            if found is None:
                continue
            form, raw = found
            readings[name]["forms"][form] += 1
            try:
                number = float(raw)
            except ValueError:
                readings[name]["unparseable"] += 1
                continue
            if not math.isfinite(number):
                readings[name]["unparseable"] += 1
            elif number == 0:
                readings[name]["zero"] += 1
            else:
                readings[name]["nonzero"].append(number)
                numbers[name] = number
        if "length" in numbers and "width" in numbers and numbers["length"] >= numbers["width"]:
            length_not_smaller += 1
    lines = ["  version element: present in %d items" % with_version]
    for name in VERSION_DIMENSIONS:
        lines.append(describe_dimension(name, readings[name]))
    lines.append("  length not smaller than width: %d items" % length_not_smaller)
    return lines


def summarise_names(items, collector):
    """Report lines about how names are carried and whether entity artefacts remain."""
    in_text = 0
    in_attribute = 0
    artefacts = 0
    for item in items:
        for name in item.findall("name"):
            value = name.get("value")
            if value is not None:
                in_attribute += 1
                title = value
            else:
                in_text += 1
                title = (name.text or "").strip()
            collector.note(title)
            if "&amp;" in title or "&#" in title:
                artefacts += 1
    return [
        "  name elements: text %d, value attribute %d" % (in_text, in_attribute),
        "  names still containing an entity marker: %d" % artefacts,
    ]


def summarise_optional_fields(items):
    """Report lines on presence and form of the optional collection fields."""
    lines = []
    for tag in COLLECTION_OPTIONAL_FIELDS:
        present = 0
        as_text = 0
        as_attribute = 0
        for item in items:
            element = item.find(tag)
            if element is None:
                continue
            present += 1
            if element.get("value") is not None:
                as_attribute += 1
            elif (element.text or "").strip():
                as_text += 1
        lines.append("  %s: present %d, text %d, value attribute %d" % (tag, present, as_text, as_attribute))
    return lines


def summarise_private_info(items, collector):
    """Report lines on the private info element and its inventory location."""
    having = 0
    attribute_names = collections.Counter()
    non_empty = 0
    distinct = set()
    longest = 0
    for item in items:
        private = item.find("privateinfo")
        if private is None:
            continue
        having += 1
        for name in private.attrib:
            attribute_names[safe_name(name)] += 1
        location = private.get("inventorylocation")
        if location is None:
            element = private.find("inventorylocation")
            location = (element.text or "") if element is not None else ""
        location = location.strip()
        if location:
            non_empty += 1
            distinct.add(location)
            longest = max(longest, len(location))
            collector.note(location)
    return [
        "  items with a private info element: %d" % having,
        format_counter("private info attribute names", attribute_names),
        "  private info with a non-empty inventorylocation: %d" % non_empty,
        "  distinct non-empty inventorylocation values: %d" % len(distinct),
        "  longest inventorylocation value: %d" % longest,
    ]


def summarise_stats(items):
    """Report lines on the stats element of collection items."""
    having = 0
    attribute_names = collections.Counter()
    for item in items:
        stats = item.find("stats")
        if stats is None:
            continue
        having += 1
        for name in stats.attrib:
            attribute_names[safe_name(name)] += 1
    return ["  items with a stats element: %d" % having, format_counter("stats attribute names", attribute_names)]


def item_key(item):
    """Stable identity of a collection entry: its collection id, else its object id."""
    collid = item.get("collid")
    if collid:
        return "c" + collid
    return "o" + (item.get("objectid") or "")


def summarise_generic_items(root, items, collector):
    """Report lines shared by every items answer: counts and element and attribute names."""
    totalitems = root.get("totalitems")
    if totalitems is None:
        total_text = "missing"
    elif totalitems.isdigit():
        total_text = totalitems
    else:
        total_text = "non-numeric"
    other_root_attributes = collections.Counter(safe_name(name) for name in root.attrib if name != "totalitems")
    item_attributes = collections.Counter()
    children = {}
    for item in items:
        for name in item.attrib:
            item_attributes[safe_name(name)] += 1
        seen = set()
        for child in item:
            tag = safe_name(child.tag)
            if tag not in children:
                children[tag] = [0, collections.Counter()]
            if tag not in seen:
                children[tag][0] += 1
                seen.add(tag)
            for name in child.attrib:
                children[tag][1][safe_name(name)] += 1
    lines = [
        "  totalitems: %s" % total_text,
        "  other root attribute names: %s" % (", ".join(sorted(other_root_attributes)) or "none"),
        "  parsed items: %d" % len(items),
        "  totalitems equals parsed items: %s" % ("yes" if total_text == str(len(items)) else "no"),
        format_counter("item attribute names", item_attributes),
    ]
    for tag in sorted(children):
        count, attributes = children[tag]
        listing = ", ".join("%s %d" % (key, attributes[key]) for key in sorted(attributes)) or "none"
        lines.append("  child element %s: in %d items, attribute names: %s" % (tag, count, listing))
    for item in items:
        for name in item.findall("name"):
            collector.note(name.get("value") or (name.text or ""))
    return lines


def summarise_collection_items(root, items, collector, label):
    """Report lines for a collection answer, and remember keys for later comparison."""
    lines = summarise_generic_items(root, items, collector)
    subtypes = collections.Counter(safe_category(item.get("subtype")) for item in items)
    owns = collections.Counter()
    objectids = collections.Counter()
    collids = collections.Counter()
    keys = set()
    key_subtypes = {}
    for item in items:
        status = item.find("status")
        owns[safe_category(status.get("own") if status is not None else None)] += 1
        if item.get("objectid"):
            objectids[item.get("objectid")] += 1
        if item.get("collid"):
            collids[item.get("collid")] += 1
        key = item_key(item)
        keys.add(key)
        key_subtypes[key] = safe_category(item.get("subtype"))
    collector.keys[label] = keys
    collector.subtypes[label] = key_subtypes
    collector.counts[label] = len(items)
    lines.append(format_counter("subtype values", subtypes))
    lines.append(format_counter("own status values", owns))
    lines.append("  repeated objectid values: %d" % sum(1 for count in objectids.values() if count > 1))
    lines.append("  repeated collid values: %d" % sum(1 for count in collids.values() if count > 1))
    lines.extend(summarise_versions(items))
    lines.extend(summarise_names(items, collector))
    lines.extend(summarise_optional_fields(items))
    lines.extend(summarise_private_info(items, collector))
    lines.extend(summarise_stats(items))
    lines.extend(compare_with_earlier_calls(label, collector))
    return lines


def compare_with_earlier_calls(label, collector):
    """Report how the expansion call and the unfiltered call relate to the base-games call."""
    lines = []
    if label == "C" and "A" in collector.keys:
        shared = len(collector.keys["A"] & collector.keys["C"])
        lines.append("  collection ids shared with call A: %d" % shared)
    if label == "D" and "A" in collector.keys and "C" in collector.keys:
        expected = collector.counts["A"] + collector.counts["C"]
        lines.append("  item count equals call A plus call C: %s" % ("yes" if collector.counts["D"] == expected else "no"))
        mislabelled = sum(
            1
            for key, subtype in collector.subtypes["D"].items()
            if subtype == "boardgame" and key in collector.keys["C"]
        )
        lines.append("  items with subtype boardgame that also appear in call C: %d" % mislabelled)
    return lines


def summarise_body(call, body, collector):
    """Return the body class and the report lines describing the body's shape."""
    body_class, root = classify_body(body)
    lines = ["  body bytes: %d" % len(body), "  body class: %s" % body_class]
    if body_class == "html":
        lowered = body.lower()
        lines.append("  mentions cloudflare: %s" % ("yes" if b"cloudflare" in lowered else "no"))
        lines.append("  has form: %s" % ("yes" if b"<form" in lowered else "no"))
    elif body_class == "xml:items":
        items = root.findall("item")
        if call.endpoint == "collection":
            lines.extend(summarise_collection_items(root, items, collector, call.label))
        else:
            lines.extend(summarise_generic_items(root, items, collector))
    elif body_class == "xml:errors":
        errors = root.findall("error")
        child_names = collections.Counter(safe_name(child.tag) for child in root)
        lines.append("  error elements: %d" % len(errors))
        lines.append(format_counter("child element names", child_names))
    elif body_class == "xml:message":
        lines.append("  message text length: %d" % len((root.text or "").strip()))
        lines.append("  root attribute names: %s" % (", ".join(sorted(safe_name(name) for name in root.attrib)) or "none"))
    elif body_class == "xml:other":
        child_names = collections.Counter(safe_name(child.tag) for child in root)
        lines.append("  root element name: %s" % safe_name(root.tag))
        lines.append(format_counter("child element names", child_names))
    return lines


def describe_redirect(headers):
    """Classify a redirect target as same host, the www variant, or another host, without printing it."""
    for name, value in headers:
        if name.lower() == "location":
            try:
                hostname = (urlsplit(value).hostname or "").lower()
            except ValueError:
                return "unparseable"
            if hostname == HOST:
                return "same host"
            if hostname.startswith("www."):
                return "www variant"
            return "relative" if not hostname else "other host"
    return "no location header"


def describe_headers(headers):
    """Report lines for the whitelisted response headers and the names of any cookies."""
    lines = []
    cookies = []
    for name, value in headers:
        lowered = name.lower()
        if lowered in SHOWN_HEADERS or lowered.startswith("x-ratelimit-"):
            lines.append("  header %s: %s" % (safe_name(lowered), safe_header_value(value)))
        elif lowered == "set-cookie":
            cookies.append(safe_name(value.split("=", 1)[0].strip()))
    if cookies:
        lines.append("  cookie names: %s" % ", ".join(sorted(cookies)))
    return lines


def render_call(call, result, collector, summarise=None):
    """Return the report lines for one call; the body is summarised by the given function or the default."""
    lines = ["call %s: %s" % (call.label, call.endpoint)]
    if result.skipped:
        lines.append("  skipped: %s" % result.skipped)
        return lines
    if result.error:
        lines.append("  error: %s" % safe_name(result.error))
        return lines
    lines.append("  status: %d" % result.status)
    lines.append("  seconds: %d" % result.seconds)
    lines.append("  answers with status 202: %d" % result.answers_202)
    if result.budget_reached:
        lines.append("  stopped polling: request budget reached")
    lines.extend(describe_headers(result.headers))
    if 300 <= result.status < 400:
        lines.append("  redirect target: %s (not followed)" % describe_redirect(result.headers))
    lines.extend((summarise or summarise_body)(call, result.body, collector))
    return lines


class Transport:
    """Sends the planned requests one at a time, spaced out and capped, to one host only."""

    def __init__(self, token, contact, clock=time.monotonic, sleep=time.sleep, limit=MAX_REQUESTS):
        self.token = token
        self.contact = contact
        self.limit = limit
        self.clock = clock
        self.sleep = sleep
        self.context = ssl.create_default_context()
        self.requests_made = 0
        self.last_finished = None

    def build_path(self, call, username, fills=None):
        """Build the request path and query for a call, filling in the username and any named blanks."""
        filled = {"username": username}
        filled.update(fills or {})
        pairs = [(name, filled[name] if value is USERNAME_PARAMETER else value) for name, value in call.parameters]
        return API_PREFIX + call.endpoint + "?" + urlencode(pairs)

    def build_headers(self, call):
        """Build the request headers; the token is only ever attached to requests for this host."""
        headers = {"Accept": "text/xml, application/xml"}
        if call.auth == "token":
            headers["Authorization"] = "Bearer " + self.token
        elif call.auth == "wrong":
            headers["Authorization"] = "Bearer " + WRONG_TOKEN
        if call.user_agent:
            agent = USER_AGENT
            if self.contact:
                agent += " (+" + self.contact + ")"
            headers["User-Agent"] = agent
        return headers

    def space_out(self):
        """Wait until at least the minimum gap has passed since the previous request finished."""
        if self.last_finished is None:
            return
        remaining = self.last_finished + MIN_GAP_SECONDS - self.clock()
        if remaining > 0:
            self.sleep(remaining)

    def exchange(self, path, headers):
        """Make one request without following redirects; return status, headers and body."""
        self.space_out()
        self.requests_made += 1
        connection = http.client.HTTPSConnection("boardgamegeek.com", timeout=REQUEST_TIMEOUT_SECONDS, context=self.context)
        try:
            connection.request("GET", path, headers=headers)
            response = connection.getresponse()
            body = response.read(MAX_BODY_BYTES + 1)
            return response.status, response.getheaders(), body
        finally:
            connection.close()
            self.last_finished = self.clock()

    def send(self, call, username, fills=None):
        """Run one planned call, polling while the service answers that it queued the request."""
        started = self.clock()
        path = self.build_path(call, username, fills)
        headers = self.build_headers(call)
        answers_202 = 0
        polls = 0
        budget_reached = False
        status, response_headers, body = None, [], b""
        while True:
            if self.requests_made >= self.limit:
                if status is None:
                    return Result(None, 0, [], b"", 0, None, "request budget reached", False)
                budget_reached = True
                break
            try:
                status, response_headers, body = self.exchange(path, headers)
            except (OSError, http.client.HTTPException) as failure:
                return Result(None, int(self.clock() - started), [], b"", answers_202, type(failure).__name__, None, False)
            if status != 202:
                break
            answers_202 += 1
            if polls >= len(POLL_WAITS):
                break
            self.sleep(POLL_WAITS[polls])
            polls += 1
        return Result(status, int(round(self.clock() - started)), response_headers, body, answers_202, None, None, budget_reached)


def run_calls(transport, username, collector, report_progress=None):
    """Run the call plan, applying the stop rules; return (report lines, stopped flag)."""
    lines = []
    stopped = False
    troubles = 0
    for index, call in enumerate(CALLS):
        result = transport.send(call, username)
        if report_progress:
            report_progress(call.label)
        lines.extend(render_call(call, result, collector))
        if result.skipped:
            continue
        if call.label in STOP_LABELS and result.status in STOP_STATUSES:
            lines.append("stop rule: status %d on call %s, remaining calls not made" % (result.status, call.label))
            stopped = True
            break
        in_trouble = result.error is not None or (result.status is not None and result.status >= 500)
        troubles = troubles + 1 if in_trouble else 0
        if troubles >= MAX_CONSECUTIVE_TROUBLES:
            lines.append("stop rule: repeated trouble, remaining calls not made")
            stopped = True
            break
    lines.append("requests made: %d of %d" % (transport.requests_made, MAX_REQUESTS))
    return lines, stopped


ART_CALLS = (
    Call("J", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("excludesubtype", "boardgameexpansion"), ("version", "1")), "token", True),
    Call("K", "collection", (("username", USERNAME_PARAMETER), ("own", "1"), ("subtype", "boardgameexpansion"), ("version", "1")), "token", True),
    Call("L", "thing", (("id", USERNAME_PARAMETER), ("stats", "1")), "token", True),
)
MAX_DOWNLOADS = MAX_IMAGE_REQUESTS - 1
DOWNLOAD_LABELS = tuple("M%d" % number for number in range(1, MAX_DOWNLOADS + 1))
ART_ITEM_FACTS = ("items", "image", "thumbnail", "minage above zero", "average present", "bayesaverage above zero", "averageweight above zero")
SIGNATURE_PNG = b"\x89PNG\r\n\x1a\n"
SIGNATURE_JPEG = b"\xff\xd8\xff"
JPEG_STANDALONE_MARKERS = frozenset([0x01] + list(range(0xD0, 0xDA)))
JPEG_NON_FRAME_MARKERS = frozenset((0xC4, 0xC8, 0xCC))


class ImageResult(typing.NamedTuple):
    """What one image request produced: status, headers and body, or why it was not made."""

    status: typing.Optional[int]
    seconds: int
    headers: list
    body: bytes
    error: typing.Optional[str]
    skipped: typing.Optional[str]


class ImageTransport:
    """Downloads images one at a time, spaced out and capped, from known image hosts only, never with a token."""

    def __init__(self, contact, clock=time.monotonic, sleep=time.sleep):
        self.contact = contact
        self.clock = clock
        self.sleep = sleep
        self.context = ssl.create_default_context()
        self.requests_made = 0
        self.last_finished = None

    def build_headers(self, with_user_agent):
        """Build the request headers: only Accept and, optionally, User-Agent; never any credential."""
        headers = {"Accept": IMAGE_ACCEPT}
        if with_user_agent:
            agent = USER_AGENT
            if self.contact:
                agent += " (+" + self.contact + ")"
            headers["User-Agent"] = agent
        return headers

    def space_out(self):
        """Wait until at least the image gap has passed since the previous image request finished."""
        if self.last_finished is None:
            return
        remaining = self.last_finished + IMAGE_GAP_SECONDS - self.clock()
        if remaining > 0:
            self.sleep(remaining)

    def exchange(self, host, target, headers):
        """Make one request without following redirects; return status, headers and at most the byte cap plus one."""
        self.space_out()
        self.requests_made += 1
        connection = http.client.HTTPSConnection(host, timeout=REQUEST_TIMEOUT_SECONDS, context=self.context)
        try:
            connection.request("GET", target, headers=headers)
            response = connection.getresponse()
            body = response.read(MAX_IMAGE_BYTES + 1)
            return response.status, response.getheaders(), body
        finally:
            connection.close()
            self.last_finished = self.clock()

    def fetch(self, host, target, with_user_agent):
        """Download one image from a known host, honouring the request budget."""
        if host not in IMAGE_HOSTS_KNOWN:
            return ImageResult(None, 0, [], b"", None, "host not allowed")
        if self.requests_made >= MAX_IMAGE_REQUESTS:
            return ImageResult(None, 0, [], b"", None, "request budget reached")
        started = self.clock()
        try:
            status, headers, body = self.exchange(host, target, self.build_headers(with_user_agent))
        except (OSError, http.client.HTTPException) as failure:
            return ImageResult(None, int(self.clock() - started), [], b"", type(failure).__name__, None)
        return ImageResult(status, int(round(self.clock() - started)), headers, body, None, None)


class ArtFacts:
    """What the art calls learn from one another, kept in memory only and never printed."""

    def __init__(self):
        self.version_images = []
        self.item_images = []
        self.thing_images = []
        self.base_ids = []
        self.expansion_ids = []
        self.requested_ids = []


def normalise_image_text(text):
    """Give a protocol-relative address its scheme so it can be split like any other."""
    stripped = text.strip()
    if stripped.startswith("//"):
        return "https:" + stripped
    return stripped


def is_protocol_relative(text):
    """True for an address that starts with two slashes and carries no scheme."""
    return text.strip().startswith("//")


def has_png_filter(text):
    """True for an address whose path asks the image service for a PNG rendition."""
    return "format(png)" in text


def split_image_url(text):
    """Return the parsed parts of an image address, or None when it cannot be split."""
    try:
        return urlsplit(normalise_image_text(text))
    except ValueError:
        return None


def classify_image_url(text):
    """Return (host class, path form class) for an image address, without keeping the address."""
    parts = split_image_url(text)
    if parts is None:
        return "other", "other"
    host = (parts.hostname or "").lower()
    path = parts.path
    if host in IMAGE_HOSTS_KNOWN:
        host_class = "cdn"
    elif host == IMAGE_HOST_SUFFIX or host.endswith("." + IMAGE_HOST_SUFFIX):
        host_class = "geekdo-other"
    else:
        host_class = "other"
    if "__original/" in path:
        form_class = "signed-original"
    elif "__" in path:
        form_class = "sized-variant"
    elif path.startswith("/images/pic"):
        form_class = "legacy-pic"
    else:
        form_class = "other"
    return host_class, form_class


def parse_download_target(text):
    """Return (host, request target) when the address is an https or protocol-relative one on a known host, else None."""
    parts = split_image_url(text)
    if parts is None or parts.scheme != "https":
        return None
    try:
        port = parts.port
    except ValueError:
        return None
    host = (parts.hostname or "").lower()
    if port is not None or host not in IMAGE_HOSTS_KNOWN:
        return None
    target = (parts.path or "/") + ("?" + parts.query if parts.query else "")
    if not is_header_safe(target):
        return None
    return host, target


def select_downloads(version_images, item_images, thing_images):
    """Choose the downloads: up to three version images, then up to three item images (or details images when none exist)."""
    chosen = []

    def take(addresses, limit):
        taken = 0
        for text in addresses:
            if taken >= limit:
                break
            target = parse_download_target(text)
            if target is None or target in chosen:
                continue
            chosen.append(target)
            taken += 1

    take(version_images, MAX_ART_VERSION_IMAGES)
    take(item_images or thing_images, MAX_ART_ITEM_IMAGES)
    return chosen


def read_image_header(data):
    """Return (format, width, height) from the first bytes of an image; width and height are None when unreadable."""
    if data.startswith(SIGNATURE_PNG):
        return "png", *read_png_size(data)
    if data.startswith(SIGNATURE_JPEG):
        return "jpeg", *read_jpeg_size(data)
    if data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        return "webp", *read_webp_size(data)
    if data.startswith(b"GIF8"):
        return "gif", None, None
    return "other", None, None


def read_png_size(data):
    """Width and height from the first chunk of a PNG, or (None, None)."""
    if len(data) < 24 or data[12:16] != b"IHDR":
        return None, None
    return struct.unpack(">II", data[16:24])


def read_jpeg_size(data):
    """Width and height from the first frame marker of a JPEG, or (None, None)."""
    position = 2
    while position + 4 <= len(data):
        if data[position] != 0xFF:
            return None, None
        marker = data[position + 1]
        if marker == 0xFF:
            position += 1
            continue
        if marker in JPEG_STANDALONE_MARKERS:
            position += 2
            continue
        if 0xC0 <= marker <= 0xCF and marker not in JPEG_NON_FRAME_MARKERS:
            if position + 9 > len(data):
                return None, None
            height, width = struct.unpack(">HH", data[position + 5:position + 9])
            return width, height
        if marker == 0xDA:
            return None, None
        position += 2 + struct.unpack(">H", data[position + 2:position + 4])[0]
    return None, None


def read_webp_size(data):
    """Width and height from the first chunk of a WebP (lossy, lossless or extended), or (None, None)."""
    kind = data[12:16]
    if kind == b"VP8 " and len(data) >= 30 and data[23:26] == b"\x9d\x01\x2a":
        width, height = struct.unpack("<HH", data[26:30])
        return width & 0x3FFF, height & 0x3FFF
    if kind == b"VP8L" and len(data) >= 25 and data[20] == 0x2F:
        bits = struct.unpack("<I", data[21:25])[0]
        return (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1
    if kind == b"VP8X" and len(data) >= 30:
        width = int.from_bytes(data[24:27], "little") + 1
        height = int.from_bytes(data[27:30], "little") + 1
        return width, height
    return None, None


def element_text(element):
    """Trimmed text of an element, empty when the element is missing."""
    if element is None:
        return ""
    return (element.text or "").strip()


def positive_value(element):
    """True when the element's value attribute is a finite number above zero."""
    if element is None:
        return False
    try:
        number = float(element.get("value", ""))
    except ValueError:
        return False
    return math.isfinite(number) and number > 0


def note_image_address(text, collector):
    """Remember an image address and its path so the guard can look for them in the report."""
    collector.note(text)
    parts = split_image_url(text)
    if parts is not None:
        collector.note(parts.path)
        if parts.query:
            collector.note(parts.path + "?" + parts.query)


def summarise_image_addresses(addresses):
    """Report lines classifying every image address seen: host class, path form, and two spelling counts."""
    hosts = collections.Counter()
    forms = collections.Counter()
    for text in addresses:
        host_class, form_class = classify_image_url(text)
        hosts[host_class] += 1
        forms[form_class] += 1
    return [
        format_counter("image address host classes", hosts),
        format_counter("image address path forms", forms),
        "  image addresses written without a scheme: %d" % sum(1 for text in addresses if is_protocol_relative(text)),
        "  image addresses asking for a PNG rendition: %d" % sum(1 for text in addresses if has_png_filter(text)),
    ]


def summarise_art_collection(label, root, collector, facts):
    """Report lines on where the owned version's image sits in a collection answer; remember addresses and ids."""
    items = root.findall("item")
    with_version = 0
    version_images = []
    item_images = []
    equal = 0
    version_thumbnails = 0
    base_ids = []
    for item in items:
        objectid = item.get("objectid") or ""
        collector.note_number(objectid)
        collector.note_number(item.get("collid"))
        for name in item.findall("name"):
            collector.note(name.get("value") or element_text(name))
        version = item.find("version/item")
        version_image = ""
        if version is not None:
            with_version += 1
            collector.note_number(version.get("id"))
            for name in version.findall("name"):
                collector.note(name.get("value") or element_text(name))
            version_image = element_text(version.find("image"))
            if element_text(version.find("thumbnail")):
                version_thumbnails += 1
                note_image_address(element_text(version.find("thumbnail")), collector)
        item_image = element_text(item.find("image"))
        if version_image:
            version_images.append(version_image)
        if item_image:
            item_images.append(item_image)
        if version_image and item_image == version_image:
            equal += 1
        if objectid and objectid not in base_ids:
            base_ids.append(objectid)
    for text in version_images + item_images:
        note_image_address(text, collector)
    if label == "J":
        facts.version_images = version_images
        facts.item_images = item_images
        facts.base_ids = base_ids
    else:
        facts.expansion_ids = base_ids
    lines = [
        "  items: %d" % len(items),
        "  items with a version element: %d" % with_version,
        "  items whose version has an image: %d" % len(version_images),
        "  items with an item-level image: %d" % len(item_images),
        "  items where the version image equals the item-level image: %d" % equal,
        "  items whose version has a thumbnail: %d" % version_thumbnails,
    ]
    lines.extend(summarise_image_addresses(version_images + item_images))
    return lines


def describe_range(numbers):
    """Smallest and largest of a list as report text, or 'none' for an empty list."""
    if not numbers:
        return "none"
    return "min %d, max %d" % (min(numbers), max(numbers))


def summarise_things(root, requested_ids, collector, facts):
    """Report lines on the per-game details answer: types, field presence, link types, missing ids."""
    items = root.findall("item")
    per_type = {}
    link_counts = collections.Counter()
    inbound_per_expansion = []
    outbound_per_base = []
    returned = set()
    for item in items:
        kind = safe_category(item.get("type"))
        facts_for_type = per_type.setdefault(kind, collections.Counter())
        facts_for_type["items"] += 1
        returned.add(item.get("id") or "")
        collector.note_number(item.get("id"))
        for name in item.findall("name"):
            collector.note(name.get("value") or element_text(name))
        for tag in ("image", "thumbnail"):
            text = element_text(item.find(tag))
            if text:
                facts_for_type[tag] += 1
                note_image_address(text, collector)
                if tag == "image":
                    facts.thing_images.append(text)
        if positive_value(item.find("minage")):
            facts_for_type["minage above zero"] += 1
        if item.find("statistics/ratings/average") is not None:
            facts_for_type["average present"] += 1
        if positive_value(item.find("statistics/ratings/bayesaverage")):
            facts_for_type["bayesaverage above zero"] += 1
        if positive_value(item.find("statistics/ratings/averageweight")):
            facts_for_type["averageweight above zero"] += 1
        inbound = 0
        outbound = 0
        for link in item.findall("link"):
            link_type = safe_category(link.get("type"))
            if link_type == "boardgameexpansion":
                if link.get("inbound") == "true":
                    inbound += 1
                    link_counts[link_type + " inbound"] += 1
                else:
                    outbound += 1
                    link_counts[link_type + " outbound"] += 1
            else:
                link_counts[link_type] += 1
        if kind == "boardgameexpansion":
            inbound_per_expansion.append(inbound)
        elif kind == "boardgame":
            outbound_per_base.append(outbound)
    lines = [
        "  items: %d" % len(items),
        "  requested ids: %d, missing from the answer: %d" % (len(requested_ids), len(set(requested_ids) - returned)),
        format_counter("item types", collections.Counter({kind: counts["items"] for kind, counts in per_type.items()})),
    ]
    for kind in sorted(per_type):
        listing = ", ".join("%s %d" % (fact, per_type[kind][fact]) for fact in ART_ITEM_FACTS)
        lines.append("  type %s: %s" % (kind, listing))
    lines.append(format_counter("link counts by type", link_counts))
    lines.append("  expansion items: inbound expansion links per item %s" % describe_range(inbound_per_expansion))
    outbound_text = "max %d" % max(outbound_per_base) if outbound_per_base else "none"
    lines.append("  base items: outbound expansion links per item %s" % outbound_text)
    return lines


def make_art_summariser(facts):
    """Build the body summariser for the art calls; other body classes use the access suite's descriptions."""

    def summarise(call, body, collector):
        body_class, root = classify_body(body)
        if body_class != "xml:items":
            return summarise_body(call, body, collector)
        lines = ["  body bytes: %d" % len(body), "  body class: %s" % body_class]
        if call.endpoint == "collection":
            lines.extend(summarise_art_collection(call.label, root, collector, facts))
        else:
            lines.extend(summarise_things(root, facts.requested_ids, collector, facts))
        return lines

    return summarise


def pick_thing_ids(facts):
    """Up to two base ids and two expansion ids in answer order, for the details call."""
    return facts.base_ids[:MAX_THING_IDS_PER_KIND] + facts.expansion_ids[:MAX_THING_IDS_PER_KIND]


def header_value(headers, wanted):
    """The first value of a response header by case-insensitive name, or None."""
    for name, value in headers:
        if name.lower() == wanted:
            return value
    return None


def classify_redirect_host(headers):
    """Classify the host a redirect points at, without printing it."""
    location = header_value(headers, "location")
    if location is None:
        return "no location header"
    parts = split_image_url(location)
    if parts is None:
        return "unparseable"
    host = (parts.hostname or "").lower()
    if not host:
        return "relative"
    if host in IMAGE_HOSTS_KNOWN:
        return "known image host"
    if host.endswith(IMAGE_HOST_SUFFIX):
        return "other geekdo image host"
    return "other host"


def image_flags(body, width, height):
    """The cap flags for an image answer: over the byte cap and over the pixel cap."""
    flags = []
    if len(body) > MAX_IMAGE_BYTES:
        flags.append("over-byte-cap")
    if width is not None and height is not None and width * height > PIXEL_CAP:
        flags.append("over-pixel-cap")
    return flags


def render_image_result(label, result):
    """Return the report lines for one image download."""
    lines = ["call %s: image download" % label]
    if result.skipped:
        lines.append("  skipped: %s" % result.skipped)
        return lines
    if result.error:
        lines.append("  error: %s" % safe_name(result.error))
        return lines
    kind, width, height = read_image_header(result.body)
    content_type = header_value(result.headers, "content-type")
    lines.append("  status: %d" % result.status)
    lines.append("  seconds: %d" % result.seconds)
    lines.append("  content-type: %s" % (safe_header_value(content_type) if content_type is not None else "none"))
    lines.append("  content-length sent: %s" % ("yes" if header_value(result.headers, "content-length") is not None else "no"))
    if 300 <= result.status < 400:
        lines.append("  redirect target: %s (not followed)" % classify_redirect_host(result.headers))
    lines.append("  body bytes: %d" % len(result.body))
    lines.append("  format: %s" % kind)
    size = "%d x %d" % (width, height) if width is not None and height is not None else "unknown"
    lines.append("  pixel size: %s" % size)
    lines.append("  flags: %s" % (", ".join(image_flags(result.body, width, height)) or "none"))
    return lines


def render_without_agent_result(first_status, result):
    """Return the report lines for the repeated download that sends no User-Agent: its status only."""
    lines = ["call N: image download without User-Agent"]
    if result.skipped:
        lines.append("  skipped: %s" % result.skipped)
    elif result.error:
        lines.append("  error: %s" % safe_name(result.error))
    else:
        lines.append("  status: %d" % result.status)
        lines.append("  same status as call M1: %s" % ("yes" if result.status == first_status else "no"))
    return lines


def run_image_calls(images, facts, report_progress=None):
    """Download the chosen images and the User-Agent-less repeat; return (report lines, stopped flag)."""
    targets = select_downloads(facts.version_images, facts.item_images, facts.thing_images)[:MAX_DOWNLOADS]
    if not targets:
        return ["image downloads: none selected"], False
    lines = []
    first_status = None
    for label, (host, target) in zip(DOWNLOAD_LABELS, targets):
        result = images.fetch(host, target, True)
        if report_progress:
            report_progress(label)
        lines.extend(render_image_result(label, result))
        if label == "M1":
            first_status = result.status
        if result.status in STOP_STATUSES:
            lines.append("stop rule: status %d on call %s, remaining calls not made" % (result.status, label))
            return lines, True
    host, target = targets[0]
    repeat = images.fetch(host, target, False)
    if report_progress:
        report_progress("N")
    lines.extend(render_without_agent_result(first_status, repeat))
    return lines, False


def run_art_calls(api, images, username, collector, report_progress=None):
    """Run the art call plan: two collections, one details call, then the image downloads; return (lines, stopped)."""
    facts = ArtFacts()
    summarise = make_art_summariser(facts)
    lines = []
    stopped = False
    troubles = 0
    for call in ART_CALLS:
        fills = None
        if call.label == "L":
            ids = pick_thing_ids(facts)
            if not ids:
                lines.extend(["call L: thing", "  skipped: no ids in the earlier answers"])
                continue
            facts.requested_ids = ids
            fills = {"id": ",".join(ids)}
        result = api.send(call, username, fills)
        if report_progress:
            report_progress(call.label)
        lines.extend(render_call(call, result, collector, summarise))
        if result.skipped:
            continue
        if call.label in ART_STOP_LABELS and result.status in STOP_STATUSES:
            lines.append("stop rule: status %d on call %s, remaining calls not made" % (result.status, call.label))
            stopped = True
            break
        in_trouble = result.error is not None or (result.status is not None and result.status >= 500)
        troubles = troubles + 1 if in_trouble else 0
        if troubles >= MAX_CONSECUTIVE_TROUBLES:
            lines.append("stop rule: repeated trouble, remaining calls not made")
            stopped = True
            break
    if not stopped:
        image_lines, stopped = run_image_calls(images, facts, report_progress)
        lines.extend(image_lines)
    lines.append("API requests made: %d of %d" % (api.requests_made, api.limit))
    lines.append("image requests made: %d of %d" % (images.requests_made, MAX_IMAGE_REQUESTS))
    return lines, stopped


def plan_lines(suite):
    """Return the call list as report lines with parameter names only; reads nothing and uses no network."""
    if suite == "art":
        return art_plan_lines()
    lines = [
        "call plan for %s (parameter names only)" % HOST,
        "at most %d requests, at least %d seconds apart, redirects are never followed" % (MAX_REQUESTS, int(MIN_GAP_SECONDS)),
    ]
    lines.extend(describe_call(call) for call in CALLS)
    return lines


def describe_call(call):
    """One plan line for a planned API call."""
    names = ", ".join(name for name, _ in call.parameters)
    auth = {"token": "the application token", "none": "no token", "wrong": "a deliberately wrong token"}[call.auth]
    agent = "sends a User-Agent" if call.user_agent else "sends no User-Agent"
    return "%s  %s  parameters: %s  uses %s  %s" % (call.label, call.endpoint, names, auth, agent)


def art_plan_lines():
    """The art call list: the API calls, then the image downloads, described without any value."""
    lines = [
        "art call plan for %s (parameter names only)" % HOST,
        "at most %d API requests, at least %d seconds apart, redirects are never followed" % (ART_MAX_API_REQUESTS, int(MIN_GAP_SECONDS)),
    ]
    lines.extend(describe_call(call) for call in ART_CALLS)
    lines.append(
        "at most %d image requests, at least %s seconds apart, to the image hosts %s only"
        % (MAX_IMAGE_REQUESTS, format_number(IMAGE_GAP_SECONDS), ", ".join(IMAGE_HOSTS_KNOWN))
    )
    lines.append(
        "%s to %s  image download  from addresses seen in calls J and L  never sends the token  sends a User-Agent  reads at most %d megabytes  never follows a redirect"
        % (DOWNLOAD_LABELS[0], DOWNLOAD_LABELS[-1], MAX_IMAGE_BYTES // (1024 * 1024))
    )
    lines.append("N  image download  repeats call M1  sends no User-Agent  records the status only")
    return lines


def print_plan(suite="access"):
    """Print the call list for a suite."""
    for line in plan_lines(suite):
        print(line)
    return EXIT_OK


def run_check(env_file, suite="access"):
    """Run the live check against the service and print the guarded report."""
    configuration = load_configuration(env_file)
    if configuration is None:
        print("not configured")
        return EXIT_NOT_CONFIGURED
    token, username, contact = configuration
    collector = Collector()
    started = time.monotonic()

    def progress(label):
        print("progress: call %s finished" % label, file=sys.stderr)

    if suite == "art":
        api = Transport(token, contact, limit=ART_MAX_API_REQUESTS)
        lines, stopped = run_art_calls(api, ImageTransport(contact), username, collector, progress)
    else:
        lines, stopped = run_calls(Transport(token, contact), username, collector, progress)
    lines.append("elapsed seconds: %d" % int(time.monotonic() - started))
    report = "\n".join(lines)
    if report_leaks(report, guarded_credentials(token, contact), collector.sensitive, [username], collector.numbers):
        print(WITHHELD_MESSAGE)
        print(WITHHELD_HINT)
        return EXIT_WITHHELD
    print(report)
    return EXIT_STOPPED if stopped else EXIT_OK


def synthetic_item(objectid, collid, subtype, title, own="1", dimensions=None, location=None):
    """Build one invented collection entry for the self-test."""
    parts = [
        '<item objecttype="thing" objectid="%s" subtype="%s" collid="%s">' % (objectid, subtype, collid),
        '<name sortindex="1">%s</name>' % title,
        "<yearpublished>2001</yearpublished>",
        "<image>https://example.org/images/%s.jpg</image>" % objectid,
        "<thumbnail>https://example.org/thumbs/%s.jpg</thumbnail>" % objectid,
        '<stats minplayers="2" maxplayers="4" playingtime="60"><rating value="N/A"/></stats>',
        '<status own="%s" prevowned="0" fortrade="0" want="0" wanttobuy="0" wishlist="0" preordered="0"/>' % own,
        "<numplays>0</numplays>",
        "<comment>Invented comment text</comment>",
    ]
    if dimensions is not None:
        width, length, depth = dimensions
        parts.append(
            '<version><item type="boardgameversion" id="900%s"><name value="Invented Edition"/>'
            '<width value="%s"/><length value="%s"/><depth value="%s"/></item></version>' % (objectid, width, length, depth)
        )
    if location is not None:
        parts.append('<privateinfo pricepaid="0.00" inventorylocation="%s"/>' % location)
    parts.append("</item>")
    return "".join(parts)


def synthetic_items_body(total, entries):
    """Wrap invented entries in an items answer."""
    return ('<?xml version="1.0" encoding="utf-8" standalone="yes"?><items totalitems="%d" termsofuse="terms" pubdate="invented">%s</items>' % (total, "".join(entries))).encode("utf-8")


def synthetic_base_entries():
    """Four entries for three invented games: one repeated object id, one not owned."""
    return [
        synthetic_item("100001", "5000001", "boardgame", "Example Game One", dimensions=("11.5", "11.5", "2.5"), location="Shelf A"),
        synthetic_item("100001", "5000002", "boardgame", "Example Game One", dimensions=("10", "13", "3"), location="Shelf A"),
        synthetic_item("100002", "5000003", "boardgame", "Example Game Two &amp;amp; More", own="0", dimensions=("0", "0", "0"), location=""),
        synthetic_item("100003", "5000004", "boardgame", "Example Game Three"),
    ]


def synthetic_result(body, status=200, headers=None, answers_202=0):
    """Wrap a synthetic body as the result of a call."""
    return Result(status, 3, headers or [("Content-Type", "text/xml; charset=utf-8")], body, answers_202, None, None, False)


class ScriptedTransport(Transport):
    """A transport that answers from a list of statuses on a fake clock; used by the self-test."""

    def __init__(self, statuses):
        super().__init__("sentinel-token-value", "")
        self.statuses = list(statuses)
        self.now = 0.0
        self.waits = []
        self.clock = lambda: self.now
        self.sleep = self.advance

    def advance(self, seconds):
        """Move the fake clock forward and remember the wait."""
        self.waits.append(seconds)
        self.now += seconds

    def exchange(self, path, headers):
        """Answer with the next scripted status instead of touching the network."""
        self.space_out()
        self.requests_made += 1
        status = self.statuses.pop(0)
        self.last_finished = self.now
        return status, [], b"<message>queued</message>" if status == 202 else b"<errors/>"


def access_self_test(check):
    """Run the access suite's summariser and the guard on invented answers and sentinel credentials."""
    token = "sentinel-token-value"
    username = "sentinel-user-name"
    contact = "https://example.org/sentinel-contact"
    collector = Collector()
    calls = {call.label: call for call in CALLS}
    expansion = synthetic_item("100004", "5000005", "boardgameexpansion", "Example Expansion")
    results = {
        "A": synthetic_result(
            synthetic_items_body(4, synthetic_base_entries()),
            headers=[
                ("Content-Type", "text/xml; charset=utf-8"),
                ("Server", "cloudflare"),
                ("X-RateLimit-Remaining", "42"),
                ("Set-Cookie", "sessionid=sentinel-cookie-value; Path=/"),
                ("Link", "https://example.org/not-whitelisted"),
            ],
        ),
        "C": synthetic_result(synthetic_items_body(1, [expansion])),
        "D": synthetic_result(synthetic_items_body(5, synthetic_base_entries() + [synthetic_item("100004", "5000005", "boardgame", "Example Expansion")])),
        "H": synthetic_result(b'<?xml version="1.0"?><errors><error><message>Invented message for sentinel-user-name</message></error></errors>', status=401),
        "I": synthetic_result(b"<!DOCTYPE html><html><head><title>Invented</title></head><body>Cloudflare notice<form></form></body></html>", status=403),
        "G": synthetic_result(b'<message>Invented queued notice</message>', status=202, answers_202=1),
        "F": synthetic_result(b'<?xml version="1.0"?><items termsofuse="terms"><item type="boardgame" id="13"><name type="primary" value="Example Public Game"/></item></items>'),
        "E": synthetic_result(b"<!DOCTYPE items [<!ENTITY invented 'x'>]><items>&invented;</items>"),
        "B": Result(None, 0, [], b"", 0, "TimeoutError", None, False),
    }
    lines = []
    for label in "ABCDEFGHI":
        result = results.get(label, Result(None, 0, [], b"", 0, None, "request budget reached", False))
        lines.extend(render_call(calls[label], result, collector))
    report = "\n".join(lines)

    expected = (
        "  totalitems: 4",
        "  parsed items: 4",
        "  totalitems equals parsed items: yes",
        "  repeated objectid values: 1",
        "  repeated collid values: 0",
        "  own status values: 0 1, 1 3",
        "  version element: present in 3 items",
        "  width: value attribute 3, text 0, non-zero 2, zero 1, unparseable 0, min 10, median 10.75, max 11.5",
        "  length not smaller than width: 2 items",
        "  names still containing an entity marker: 1",
        "  items with a private info element: 3",
        "  private info with a non-empty inventorylocation: 2",
        "  distinct non-empty inventorylocation values: 1",
        "  longest inventorylocation value: 7",
        "  collection ids shared with call A: 0",
        "  item count equals call A plus call C: yes",
        "  items with subtype boardgame that also appear in call C: 1",
        "  header x-ratelimit-remaining: 42",
        "  cookie names: sessionid",
        "  error elements: 1",
        "  mentions cloudflare: yes",
        "  error: TimeoutError",
        "  body class: xml:errors",
        "  body class: html",
        "  body class: other",
        "  body class: xml:message",
        "  answers with status 202: 1",
    )
    for fragment in expected:
        check("report keeps " + fragment, fragment in report)

    hostile_text = "<?xml version='1.0'?><!DOCTYPE items [<!ENTITY invented 'x'>]><items>&invented;</items>"
    clean_text = "<?xml version='1.0'?><items totalitems='0'></items>"
    for encoding in ("utf-16", "utf-16-le", "utf-16-be", "utf-32", "utf-32-le", "utf-32-be"):
        check("a doctype in " + encoding + " is refused", classify_body(hostile_text.encode(encoding)) == ("other", None))
        check("a clean document in " + encoding + " is refused", classify_body(clean_text.encode(encoding)) == ("other", None))
    check("a byte-order mark alone is refused", classify_body(b"\xff\xfe<") == ("other", None))
    check("a body holding a NUL byte is refused", classify_body(clean_text.encode("utf-8") + b"\x00") == ("other", None))
    check("a doctype in plain text is refused", classify_body(hostile_text.encode("utf-8"))[0] == "other")
    check("a doctype in mixed case is refused", classify_body(b"<!DocType items><items></items>")[0] == "other")
    check("a doctype is found by the strict parse", declares_doctype_or_entity(b"<!DOCTYPE items><items/>"))
    check("an entity declaration is found by the strict parse", declares_doctype_or_entity(b"<!DOCTYPE items [<!ENTITY a 'b'>]><items/>"))
    check("a clean document passes the strict parse", not declares_doctype_or_entity(clean_text.encode("utf-8")))
    check("a clean utf-8 document is still read", classify_body(clean_text.encode("utf-8"))[0] == "xml:items")
    check("a clean utf-8 document with a byte-order mark is still read", classify_body(b"\xef\xbb\xbf" + clean_text.encode("utf-8"))[0] == "xml:items")

    skipped_lines = render_call(calls["I"], Result(None, 0, [], b"", 0, None, "request budget reached", False), Collector())
    check("a skipped call is recorded as skipped", "  skipped: request budget reached" in skipped_lines)

    credentials = guarded_credentials(token, contact)
    names = [username]
    forbidden = (
        "Example Game One",
        "Example Game Two",
        "Example Game Three",
        "Example Expansion",
        "Example Public Game",
        "Shelf A",
        "sentinel-cookie-value",
        "sentinel-contact",
        "example.org",
        "Invented",
    )
    for value in forbidden:
        check("report drops " + value, value not in report)
    for credential in credentials + names:
        check("report drops credential " + credential, credential not in report)
    check("report holds no url marker", "http" not in report.lower())
    check("report holds no markup", "<" not in report)
    check("collector remembered the invented titles", {"Example Game One", "Example Expansion", "Shelf A"} <= collector.sensitive)
    check("guard passes the clean report", not report_leaks(report, credentials, collector.sensitive, names))
    check("guard fires on a token", report_leaks(report + " " + token, credentials, collector.sensitive, names))
    check("guard fires on a username", report_leaks(report + " " + username.upper(), credentials, collector.sensitive, names))
    check("guard fires on a contact address", report_leaks(report + " " + contact, credentials, collector.sensitive, names))
    check("guard fires on a remembered title", report_leaks(report + " Example Game One", credentials, collector.sensitive, names))
    check("guard fires on a remembered location", report_leaks(report + " Shelf A", credentials, collector.sensitive, names))
    check("guard fires on a url marker", report_leaks(report + " https", credentials, collector.sensitive, names))
    check("guard fires on markup", report_leaks(report + " <", credentials, collector.sensitive, names))
    check("guard matches short values as whole words", report_leaks("Go now", [], {"Go"}) and not report_leaks("Gone", [], {"Go"}))
    check("guard passes a short username inside longer words", not report_leaks("status items stats", [], set(), ["us"]))
    check("guard fires on a short username as a whole word", report_leaks("seen by us today", [], set(), ["us"]))
    check("guard fires on a short username in any case", report_leaks("seen by US today", [], set(), ["us"]))
    check("guard fires on a long username inside a longer word", report_leaks("status", [], set(), ["stat"]))
    check("guard fires on a short token inside a longer word", report_leaks("status", ["us"], set()))
    check("guard credentials are the token, the contact address and the fixed wrong token", guarded_credentials(token, contact) == [token, contact, WRONG_TOKEN])

    plan_text = " ".join(name for call in CALLS for name, _ in call.parameters)
    check("plan names showprivate", "showprivate" in plan_text)
    check("plan never mentions the www host", ("www." + HOST) not in repr(CALLS))

    transport = Transport("x", "")
    headers = transport.build_headers(calls["G"])
    check("call without a User-Agent sends none", "User-Agent" not in headers)
    check("call without a token sends none", "Authorization" not in transport.build_headers(calls["H"]))
    check("wrong-token call uses the fixed value", transport.build_headers(calls["I"])["Authorization"] == "Bearer " + WRONG_TOKEN)

    queued = ScriptedTransport([202] * 8 + [200] * 20)
    queued_result = queued.send(CALLS[0], "invented")
    check("queued answers are polled at most six times", queued_result.answers_202 == 7 and queued.requests_made == 7)
    check("poll waits follow the schedule", [wait for wait in queued.waits if wait >= 5] == list(POLL_WAITS))

    refused = ScriptedTransport([200, 200, 401])
    refused_lines, refused_stopped = run_calls(refused, "invented", Collector())
    check("a refusal stops the run", refused_stopped and refused.requests_made == 3)
    check("a refusal is recorded", any(line.startswith("stop rule: status 401") for line in refused_lines))

    busy = ScriptedTransport([202] * 40)
    busy_lines, busy_stopped = run_calls(busy, "invented", Collector())
    check("the request cap holds", busy.requests_made == MAX_REQUESTS and not busy_stopped)
    check("calls beyond the cap are skipped", "  skipped: request budget reached" in busy_lines)
    check("requests are spaced out", all(wait >= 0 for wait in busy.waits) and busy.now >= MIN_GAP_SECONDS * (MAX_REQUESTS - 1))
    check("the access plan lists labels A to I", [line.split()[0] for line in plan_lines("access")[2:]] == list("ABCDEFGHI"))


CDN_ORIGIN = "https://cf.geekdo-images.com"
ART_VERSION_ONE = CDN_ORIGIN + "/fixture/__original/img/aaa/pic100001.png"
ART_VERSION_TWO = "//cf.geekdo-images.com/fixture/__opengraph/img/bbb/pic100002.png"
ART_VERSION_THREE = CDN_ORIGIN + "/fixture/__original/img/ccc/filters:format(png)/pic100003.png"
ART_ITEM_TWO = CDN_ORIGIN + "/images/pic100102.png"
ART_ITEM_THREE = "https://cf2.geekdo-images.com/fixture/pic100003.jpg"
ART_ITEM_FOUR = "https://example.org/fixture/pic100004.png"
ART_THING_IMAGE = CDN_ORIGIN + "/fixture/__original/img/ddd/pic100001.png"


def synthetic_art_item(objectid, collid, subtype, title, version_image=None, version_thumbnail=None, item_image=None):
    """Build one invented collection entry that carries the owned version's image, for the art self-test."""
    parts = [
        '<item objecttype="thing" objectid="%s" subtype="%s" collid="%s">' % (objectid, subtype, collid),
        '<name sortindex="1">%s</name>' % title,
    ]
    if item_image is not None:
        parts.append("<image>%s</image>" % item_image)
    parts.append('<status own="1"/>')
    if version_image is not None:
        parts.append('<version><item type="boardgameversion" id="900%s">' % objectid)
        if version_thumbnail is not None:
            parts.append("<thumbnail>%s</thumbnail>" % version_thumbnail)
        parts.append('<image>%s</image><name value="Invented Edition"/></item></version>' % version_image)
    parts.append("</item>")
    return "".join(parts)


def synthetic_art_base_body():
    """An invented base-game collection: four entries, three with a version image, one equal to its item image."""
    entries = [
        synthetic_art_item("100001", "5000001", "boardgame", "Example Game One", ART_VERSION_ONE, ART_VERSION_ONE, ART_VERSION_ONE),
        synthetic_art_item("100002", "5000002", "boardgame", "Example Game Two", ART_VERSION_TWO, ART_VERSION_TWO, ART_ITEM_TWO),
        synthetic_art_item("100003", "5000003", "boardgame", "Example Game Three", ART_VERSION_THREE, None, ART_ITEM_THREE),
        synthetic_art_item("100004", "5000004", "boardgame", "Example Game Four", None, None, ART_ITEM_FOUR),
    ]
    return synthetic_items_body(4, entries)


def synthetic_art_expansion_body():
    """An invented expansion collection: two entries, one with an item-level image and no version."""
    entries = [
        synthetic_art_item("100005", "5000005", "boardgameexpansion", "Example Expansion One", item_image=ART_ITEM_FOUR),
        synthetic_art_item("100006", "5000006", "boardgameexpansion", "Example Expansion Two"),
    ]
    return synthetic_items_body(2, entries)


def synthetic_art_thing_body():
    """An invented details answer: one base game with expansion and other links, one expansion with an inbound link."""
    base = (
        '<item type="boardgame" id="100001"><thumbnail>%s</thumbnail><image>%s</image>'
        '<name type="primary" sortindex="1" value="Example Game One"/><minage value="8"/>'
        '<link type="boardgamecategory" id="1" value="Invented Category"/>'
        '<link type="boardgamemechanic" id="2" value="Invented Mechanic"/>'
        '<link type="boardgamedesigner" id="3" value="Invented Designer"/>'
        '<link type="boardgameexpansion" id="100005" value="Example Expansion One"/>'
        '<link type="boardgameexpansion" id="100006" value="Example Expansion Two"/>'
        '<statistics page="1"><ratings><average value="7.1"/><bayesaverage value="6.5"/><averageweight value="2.4"/></ratings></statistics>'
        "</item>" % (ART_THING_IMAGE, ART_THING_IMAGE)
    )
    expansion = (
        '<item type="boardgameexpansion" id="100005"><image>%s</image>'
        '<name type="primary" sortindex="1" value="Example Expansion One"/><minage value="0"/>'
        '<link type="boardgameexpansion" id="100001" value="Example Game One" inbound="true"/>'
        '<link type="boardgamedesigner" id="3" value="Invented Designer"/>'
        '<statistics page="1"><ratings><average value="6.8"/><bayesaverage value="0"/><averageweight value="0"/></ratings></statistics>'
        "</item>" % ART_THING_IMAGE
    )
    return synthetic_items_body(2, [base, expansion])


def png_header(width, height):
    """The first bytes of a PNG of the given size, as far as a reader needs them."""
    return SIGNATURE_PNG + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", width, height) + b"\x08\x06\x00\x00\x00" + b"\x00\x00\x00\x00"


def jpeg_header(width, height, frame_marker=0xC2):
    """The first bytes of a JPEG: an application segment, a table segment that must be skipped, then a frame header."""
    application = b"\xff\xe0" + struct.pack(">H", 6) + b"JFIF"
    tables = b"\xff\xc4" + struct.pack(">H", 5) + b"\x00\x00\x00"
    frame = bytes([0xFF, frame_marker]) + struct.pack(">HBHH", 11, 8, height, width) + b"\x03\x01\x11\x00\x02\x11\x00"
    return SIGNATURE_JPEG[:2] + application + tables + frame


def webp_header(kind, width, height):
    """The first bytes of a WebP of the given kind (lossy, lossless or extended) and size."""
    if kind == "lossy":
        chunk = b"VP8 " + struct.pack("<I", 10) + b"\x00\x00\x00\x9d\x01\x2a" + struct.pack("<HH", width, height)
    elif kind == "lossless":
        bits = (width - 1) | ((height - 1) << 14)
        chunk = b"VP8L" + struct.pack("<I", 5) + b"\x2f" + struct.pack("<I", bits)
    else:
        chunk = b"VP8X" + struct.pack("<I", 10) + b"\x00\x00\x00\x00" + (width - 1).to_bytes(3, "little") + (height - 1).to_bytes(3, "little")
    return b"RIFF" + struct.pack("<I", len(chunk) + 4) + b"WEBP" + chunk


class ScriptedApiTransport(Transport):
    """An API transport that answers from a list of (status, body) pairs on a fake clock; used by the art self-test."""

    def __init__(self, answers):
        super().__init__("sentinel-token-value", "", limit=ART_MAX_API_REQUESTS)
        self.answers = list(answers)
        self.now = 0.0
        self.paths = []
        self.starts = []
        self.clock = lambda: self.now
        self.sleep = self.advance

    def advance(self, seconds):
        """Move the fake clock forward."""
        self.now += seconds

    def exchange(self, path, headers):
        """Answer with the next scripted pair instead of touching the network."""
        self.space_out()
        self.requests_made += 1
        self.paths.append(path)
        self.starts.append(self.now)
        status, body = self.answers.pop(0)
        self.last_finished = self.now
        return status, [("Content-Type", "text/xml")], body


class ScriptedImageTransport(ImageTransport):
    """An image transport that answers by request target on a fake clock; used by the art self-test."""

    def __init__(self, answers, contact=""):
        super().__init__(contact)
        self.answers = answers
        self.now = 0.0
        self.starts = []
        self.seen_headers = []
        self.clock = lambda: self.now
        self.sleep = self.advance

    def advance(self, seconds):
        """Move the fake clock forward."""
        self.now += seconds

    def exchange(self, host, target, headers):
        """Answer by target and by whether a User-Agent was sent, instead of touching the network."""
        self.space_out()
        self.requests_made += 1
        self.starts.append(self.now)
        self.seen_headers.append(dict(headers))
        self.last_finished = self.now
        return self.answers.get((target, "User-Agent" in headers), (404, [], b""))


def split_report_blocks(lines):
    """Group report lines under the label of the call they belong to."""
    blocks = {}
    current = None
    for line in lines:
        if line.startswith("call "):
            current = line[5:].split(":")[0]
            blocks[current] = []
        if current is not None:
            blocks[current].append(line)
    return blocks


def art_self_test(check):
    """Run the art suite's summarisers, header reader, transports and output guard on invented data."""
    token = "sentinel-token-value"
    username = "sentinel-user-name"
    contact = "https://example.org/sentinel-contact"
    png_body = png_header(800, 600)
    jpeg_body = jpeg_header(1200, 900)
    webp_body = webp_header("lossless", 500, 400)
    forbidden_text = {
        "address": ART_VERSION_ONE,
        "scheme-less address": ART_VERSION_TWO,
        "path": "/fixture/__original/img/aaa/pic100001.png",
        "title": "Example Game One",
        "version name": "Invented Edition",
        "id": "100001",
        "collection id": "5000002",
    }

    answers = {
        ("/fixture/__original/img/aaa/pic100001.png", True): (200, [("Content-Type", "image/png"), ("Content-Length", str(len(png_body)))], png_body),
        ("/fixture/__opengraph/img/bbb/pic100002.png", True): (200, [("Content-Type", "image/jpeg")], jpeg_body),
        ("/fixture/__original/img/ccc/filters:format(png)/pic100003.png", True): (200, [("Content-Type", "image/webp")], webp_body),
        ("/images/pic100102.png", True): (302, [("Location", "https://example.org/moved")], b""),
        ("/fixture/__original/img/aaa/pic100001.png", False): (403, [], b""),
    }
    api = ScriptedApiTransport([(200, synthetic_art_base_body()), (200, synthetic_art_expansion_body()), (200, synthetic_art_thing_body())])
    images = ScriptedImageTransport(answers, contact)
    collector = Collector()
    lines, stopped = run_art_calls(api, images, "invented", collector)
    report = "\n".join(lines)
    blocks = split_report_blocks(lines)

    check("a complete art run is not stopped", not stopped)
    expected = {
        "J": (
            "  items: 4",
            "  items with a version element: 3",
            "  items whose version has an image: 3",
            "  items with an item-level image: 4",
            "  items where the version image equals the item-level image: 1",
            "  items whose version has a thumbnail: 2",
            "  image address host classes: cdn 5, geekdo-other 1, other 1",
            "  image address path forms: legacy-pic 1, other 2, signed-original 3, sized-variant 1",
            "  image addresses written without a scheme: 1",
            "  image addresses asking for a PNG rendition: 1",
        ),
        "K": (
            "  items: 2",
            "  items with a version element: 0",
            "  items whose version has an image: 0",
            "  items with an item-level image: 1",
            "  image address host classes: other 1",
        ),
        "L": (
            "  items: 2",
            "  requested ids: 4, missing from the answer: 2",
            "  item types: boardgame 1, boardgameexpansion 1",
            "  type boardgame: items 1, image 1, thumbnail 1, minage above zero 1, average present 1, bayesaverage above zero 1, averageweight above zero 1",
            "  type boardgameexpansion: items 1, image 1, thumbnail 0, minage above zero 0, average present 1, bayesaverage above zero 0, averageweight above zero 0",
            "  link counts by type: boardgamecategory 1, boardgamedesigner 2, boardgameexpansion inbound 1, boardgameexpansion outbound 2, boardgamemechanic 1",
            "  expansion items: inbound expansion links per item min 1, max 1",
            "  base items: outbound expansion links per item max 2",
        ),
        "M1": ("  status: 200", "  content-type: image/png", "  content-length sent: yes", "  body bytes: %d" % len(png_body), "  format: png", "  pixel size: 800 x 600", "  flags: none"),
        "M2": ("  status: 200", "  content-type: image/jpeg", "  content-length sent: no", "  format: jpeg", "  pixel size: 1200 x 900", "  flags: none"),
        "M3": ("  status: 200", "  format: webp", "  pixel size: 500 x 400"),
        "M4": ("  status: 302", "  redirect target: other host (not followed)", "  body bytes: 0", "  format: other", "  pixel size: unknown"),
        "N": ("  status: 403", "  same status as call M1: no"),
    }
    for label, fragments in expected.items():
        for fragment in fragments:
            check("art report block %s keeps %s" % (label, fragment), fragment in blocks.get(label, []))
    check("the report ends with the request counts", lines[-2:] == ["API requests made: 3 of %d" % ART_MAX_API_REQUESTS, "image requests made: 5 of %d" % MAX_IMAGE_REQUESTS])
    check("the details call asks for the first two base ids and the expansion ids, with statistics and no type", "id=100001%2C100002%2C100005%2C100006" in api.paths[2] and "stats=1" in api.paths[2] and "type=" not in api.paths[2])
    check("the collection calls ask for the owned version", all("version=1" in path for path in api.paths[:2]))
    check("api requests are at least six seconds apart", all(later - earlier >= MIN_GAP_SECONDS for earlier, later in zip(api.starts, api.starts[1:])))
    check("image requests are at least the image gap apart", all(later - earlier >= IMAGE_GAP_SECONDS for earlier, later in zip(images.starts, images.starts[1:])))
    check("an image request carries only Accept and User-Agent, or Accept alone", all(set(headers) <= {"Accept", "User-Agent"} for headers in images.seen_headers) and set(images.seen_headers[-1]) == {"Accept"})
    check("no image request carries any credential", all(token not in " ".join(headers.values()) for headers in images.seen_headers))
    check("the API request carries the token", "Authorization" in api.build_headers(ART_CALLS[0]))
    check("the image headers never hold an Authorization entry", "Authorization" not in images.build_headers(True) and "Authorization" not in images.build_headers(False))

    for name, value in forbidden_text.items():
        check("the art report drops the " + name, value not in report)
    check("the art report holds no url marker", "http" not in report.lower())
    check("the art report holds no markup", "<" not in report)
    check("the collector remembered the invented addresses", {ART_VERSION_ONE, ART_VERSION_TWO, "/fixture/__original/img/aaa/pic100001.png"} <= collector.sensitive)
    check("the collector remembered the invented titles", {"Example Game One", "Invented Edition", "Example Expansion One"} <= collector.sensitive)
    check("the collector remembered the invented ids", {"100001", "5000002", "900100001", "100006"} <= collector.numbers)
    credentials = guarded_credentials(token, contact)
    names = [username]

    def guard(text):
        """True when the output guard would withhold a report made of this text."""
        return report_leaks(text, credentials, collector.sensitive, names, collector.numbers)

    check("the guard passes the clean art report", not guard(report))
    for name, value in forbidden_text.items():
        check("the guard fires on the " + name, guard(report + " " + value))
    check("the guard fires on an invented id as a whole number", guard("seen 5000004 here"))
    check("the guard leaves an id inside a longer number alone", not guard("seen 50000049 here"))

    check("a png header is read", read_image_header(png_body) == ("png", 800, 600))
    check("a jpeg header is read past the table segment", read_image_header(jpeg_body) == ("jpeg", 1200, 900))
    check("a baseline jpeg header is read", read_image_header(jpeg_header(640, 480, 0xC0)) == ("jpeg", 640, 480))
    check("a lossy webp header is read", read_image_header(webp_header("lossy", 320, 240)) == ("webp", 320, 240))
    check("a lossless webp header is read", read_image_header(webp_body) == ("webp", 500, 400))
    check("an extended webp header is read", read_image_header(webp_header("extended", 1024, 768)) == ("webp", 1024, 768))
    check("a gif is named without a size", read_image_header(b"GIF89a\x01\x00\x01\x00") == ("gif", None, None))
    check("other bytes are named other", read_image_header(b"not an image at all") == ("other", None, None))
    check("a cut-off png has no size", read_image_header(SIGNATURE_PNG + b"\x00") == ("png", None, None))
    check("a cut-off jpeg has no size", read_image_header(SIGNATURE_JPEG + b"\xe0") == ("jpeg", None, None))
    check("an empty body is named other", read_image_header(b"") == ("other", None, None))

    wide = ImageResult(200, 1, [("Content-Type", "image/png")], png_header(7000, 6000), None, None)
    check("a very large picture is flagged over the pixel cap", "  flags: over-pixel-cap" in render_image_result("M1", wide))
    heavy = ImageResult(200, 1, [("Content-Type", "image/png")], png_header(100, 100) + bytes(13 * 1024 * 1024), None, None)
    check("a very heavy answer is flagged over the byte cap", "  flags: over-byte-cap" in render_image_result("M1", heavy))
    check("the pixel cap is a count of pixels", PIXEL_CAP == 36_000_000 and MAX_IMAGE_BYTES == 12 * 1024 * 1024)

    check("a signed original address is classified", classify_image_url(ART_VERSION_ONE) == ("cdn", "signed-original"))
    check("a sized variant without a scheme is classified", classify_image_url(ART_VERSION_TWO) == ("cdn", "sized-variant"))
    check("a legacy picture address is classified", classify_image_url(ART_ITEM_TWO) == ("cdn", "legacy-pic"))
    check("another geekdo host is classified", classify_image_url(ART_ITEM_THREE) == ("geekdo-other", "other"))
    check("another host is classified", classify_image_url(ART_ITEM_FOUR) == ("other", "other"))
    check("a look-alike host is not a geekdo host", classify_image_url("https://notgeekdo-images.com/x")[0] == "other")

    check("a known host with https is downloadable", parse_download_target(ART_VERSION_ONE) == ("cf.geekdo-images.com", "/fixture/__original/img/aaa/pic100001.png"))
    check("a scheme-less address is downloaded over https", parse_download_target(ART_VERSION_TWO) is not None)
    check("a plain-text scheme is refused", parse_download_target("http://cf.geekdo-images.com/x.png") is None)
    check("another host is refused", parse_download_target(ART_ITEM_THREE) is None and parse_download_target(ART_ITEM_FOUR) is None)
    check("an explicit port is refused", parse_download_target("https://cf.geekdo-images.com:8443/x.png") is None)
    check("a space in the path is refused", parse_download_target("https://cf.geekdo-images.com/a b.png") is None)
    many = ["https://cf.geekdo-images.com/fixture/pic%d.png" % number for number in range(100010, 100030)]
    check("at most three version images and three item images are chosen", len(select_downloads(many, many[10:], [])) == 6)
    check("the version images come first and duplicates are dropped", select_downloads(many[:2], many[:4], [])[:3] == [parse_download_target(many[0]), parse_download_target(many[1]), parse_download_target(many[2])])
    check("details images are used when the collection has no item images", select_downloads([], [], many[:2]) == [parse_download_target(many[0]), parse_download_target(many[1])])
    check("details images are ignored when the collection has item images", select_downloads([], [ART_ITEM_FOUR], many[:2]) == [])

    probe = ScriptedImageTransport({})
    refused = probe.fetch("example.org", "/x.png", True)
    check("a host outside the allowlist is never contacted", refused.skipped == "host not allowed" and probe.requests_made == 0)
    for _ in range(MAX_IMAGE_REQUESTS + 3):
        probe.fetch("cf.geekdo-images.com", "/x.png", True)
    check("the image request cap holds", probe.requests_made == MAX_IMAGE_REQUESTS)
    check("capped image requests stay spaced out", all(later - earlier >= IMAGE_GAP_SECONDS for earlier, later in zip(probe.starts, probe.starts[1:])))

    stopped_api = ScriptedApiTransport([(429, b"<errors/>")])
    stopped_images = ScriptedImageTransport(answers)
    stopped_lines, stopped_flag = run_art_calls(stopped_api, stopped_images, "invented", Collector())
    check("a throttle on the first call stops the run", stopped_flag and stopped_api.requests_made == 1 and any(line.startswith("stop rule: status 429 on call J") for line in stopped_lines))
    check("a stopped run downloads nothing", stopped_images.requests_made == 0)

    image_stop = ScriptedImageTransport({("/fixture/__original/img/aaa/pic100001.png", True): (429, [], b"")})
    image_api = ScriptedApiTransport([(200, synthetic_art_base_body()), (200, synthetic_art_expansion_body()), (200, synthetic_art_thing_body())])
    image_lines, image_flagged = run_art_calls(image_api, image_stop, "invented", Collector())
    check("a throttled image stops the downloads", image_flagged and image_stop.requests_made == 1 and any(line.startswith("stop rule: status 429 on call M1") for line in image_lines))

    queued_api = ScriptedApiTransport([(202, b"<message>queued</message>")] * 40)
    queued_lines, queued_stopped = run_art_calls(queued_api, ScriptedImageTransport({}), "invented", Collector())
    check("the art API request cap holds", queued_api.requests_made == ART_MAX_API_REQUESTS and not queued_stopped)
    check("a details call with no ids is skipped", "  skipped: no ids in the earlier answers" in queued_lines)

    art_plan = plan_lines("art")
    art_plan_text = "\n".join(art_plan)
    check("the art plan lists J, K, L, M1 and N", all(any(line.startswith(label + " ") for line in art_plan) for label in ("J", "K", "L", "M1", "N")))
    check("the art plan shows no value and no address", "=" not in art_plan_text and "http" not in art_plan_text.lower())
    check("the art plan names the id and statistics parameters and no type", "parameters: id, stats" in art_plan_text and "type" not in art_plan[4])
    check("the art plan states the caps", "at most %d API requests" % ART_MAX_API_REQUESTS in art_plan_text and "at most %d image requests" % MAX_IMAGE_REQUESTS in art_plan_text)


def run_self_test(suites=SUITES):
    """Run the self-test of the chosen suites, or of both when none is chosen."""
    failures = []

    def check(name, condition):
        if not condition:
            failures.append(name)

    if "access" in suites:
        access_self_test(check)
    if "art" in suites:
        art_self_test(check)
    if failures:
        for name in failures:
            print("FAIL: " + name)
        return 1
    print("self-test passed")
    return EXIT_OK


def build_parser():
    """Build the command line parser."""
    parser = argparse.ArgumentParser(prog="bgg-access-check.py", description="Shape-only BoardGameGeek access check.")
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--plan", action="store_true", help="print the call list; no network, reads nothing")
    modes.add_argument("--self-test", action="store_true", help="check the summariser and guard on synthetic data")
    modes.add_argument("--run", action="store_true", help="ask the service and print the guarded report")
    parser.add_argument("--suite", choices=SUITES, default=None, help="which call list to use: access (default) or art")
    parser.add_argument("--env-file", default=DEFAULT_ENV_FILE, help="env file holding the BoardGameGeek settings")
    return parser


def main(argv):
    """Entry point: dispatch to the chosen mode and return the exit code."""
    parser = build_parser()
    try:
        arguments = parser.parse_args(argv)
    except SystemExit as exit_request:
        return EXIT_USAGE if exit_request.code else EXIT_OK
    if arguments.self_test:
        return run_self_test(SUITES if arguments.suite is None else (arguments.suite,))
    suite = arguments.suite or "access"
    if arguments.plan:
        return print_plan(suite)
    return run_check(arguments.env_file, suite)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
