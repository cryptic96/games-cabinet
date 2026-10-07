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

Modes:
  --plan       print the call list; reads nothing and uses no network
  --self-test  run the summariser and the output guard on synthetic data
  --run        ask BoardGameGeek; reads the token, username and optional
               contact address from the env file (default
               /etc/cabinet/cabinet.env), never from the command line

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

EXIT_OK = 0
EXIT_USAGE = 2
EXIT_NOT_CONFIGURED = 3
EXIT_WITHHELD = 4
EXIT_STOPPED = 5

WITHHELD_MESSAGE = "output withheld: it would have contained a sensitive value"
WITHHELD_HINT = (
    "hint: the report matched the token, the contact address, the username (a name under four characters "
    "only as a whole word) or a title or location seen in the answers"
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


def report_leaks(report, credentials, values, names=()):
    """True when the report holds a credential, a username, a remembered value, a URL marker or markup."""
    lowered = report.lower()
    if "http" in lowered or "<" in report:
        return True
    for credential in credentials:
        if credential and credential.lower() in lowered:
            return True
    if any(contains_name(report, name) for name in names):
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


def render_call(call, result, collector):
    """Return the report lines for one call."""
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
    lines.extend(summarise_body(call, result.body, collector))
    return lines


class Transport:
    """Sends the planned requests one at a time, spaced out and capped, to one host only."""

    def __init__(self, token, contact, clock=time.monotonic, sleep=time.sleep):
        self.token = token
        self.contact = contact
        self.clock = clock
        self.sleep = sleep
        self.context = ssl.create_default_context()
        self.requests_made = 0
        self.last_finished = None

    def build_path(self, call, username):
        """Build the request path and query for a call, filling in the username."""
        pairs = [(name, username if value is USERNAME_PARAMETER else value) for name, value in call.parameters]
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

    def send(self, call, username):
        """Run one planned call, polling while the service answers that it queued the request."""
        started = self.clock()
        path = self.build_path(call, username)
        headers = self.build_headers(call)
        answers_202 = 0
        polls = 0
        budget_reached = False
        status, response_headers, body = None, [], b""
        while True:
            if self.requests_made >= MAX_REQUESTS:
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


def print_plan():
    """Print the call list with parameter names only; reads nothing and uses no network."""
    print("call plan for %s (parameter names only)" % HOST)
    print("at most %d requests, at least %d seconds apart, redirects are never followed" % (MAX_REQUESTS, int(MIN_GAP_SECONDS)))
    for call in CALLS:
        names = ", ".join(name for name, _ in call.parameters)
        auth = {"token": "the application token", "none": "no token", "wrong": "a deliberately wrong token"}[call.auth]
        agent = "sends a User-Agent" if call.user_agent else "sends no User-Agent"
        print("%s  %s  parameters: %s  uses %s  %s" % (call.label, call.endpoint, names, auth, agent))
    return EXIT_OK


def run_check(env_file):
    """Run the live check against the service and print the guarded report."""
    configuration = load_configuration(env_file)
    if configuration is None:
        print("not configured")
        return EXIT_NOT_CONFIGURED
    token, username, contact = configuration
    collector = Collector()
    transport = Transport(token, contact)
    started = time.monotonic()

    def progress(label):
        print("progress: call %s finished" % label, file=sys.stderr)

    lines, stopped = run_calls(transport, username, collector, progress)
    lines.append("elapsed seconds: %d" % int(time.monotonic() - started))
    report = "\n".join(lines)
    if report_leaks(report, guarded_credentials(token, contact), collector.sensitive, [username]):
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


def run_self_test():
    """Run the summariser and the guard on invented answers and sentinel credentials."""
    failures = []

    def check(name, condition):
        if not condition:
            failures.append(name)

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
    parser.add_argument("--env-file", default=DEFAULT_ENV_FILE, help="env file holding the BoardGameGeek settings")
    return parser


def main(argv):
    """Entry point: dispatch to the chosen mode and return the exit code."""
    parser = build_parser()
    try:
        arguments = parser.parse_args(argv)
    except SystemExit as exit_request:
        return EXIT_USAGE if exit_request.code else EXIT_OK
    if arguments.plan:
        return print_plan()
    if arguments.self_test:
        return run_self_test()
    return run_check(arguments.env_file)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
