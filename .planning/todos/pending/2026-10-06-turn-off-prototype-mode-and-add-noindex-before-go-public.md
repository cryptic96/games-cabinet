---
created: 2026-10-06T09:48:44.000Z
title: Add noindex before go-public
area: security
severity: major
files:
  - Cabinet.Service/Pages/Shared/_Layout.cshtml
---

## Problem

The committed appsettings.json turns the prototype on (Prototype:Enabled true), so the deployed site serves the invented sample collections and the sample switcher, and the pages carry no noindex. That is harmless while the route is limited to the home network and VPN, but it must not reach the public internet. Raised by the layout-prototype code review (info item on the prototype default) and the security audit's open items.

## Solution

Part of the Phase 8 go-public checklist: set the prototype off in the committed defaults (or remove the prototype entirely per the prototype removal list in the release summary of the layout prototype), add a robots noindex until the owner decides the site may be indexed, and add a test that the public page shows neither sample links nor the prototype status line when the setting is off.

## Status (2026-10-07, Phase 3 release v0.3.0)

The prototype part is done: the committed default is `Prototype:Enabled` false, `appsettings.Development.json` turns it on for local development only, and the sample catalog is disabled in Production whatever the env file says (tests cover the page with the setting on and off). The deployed v0.3.0 serves only the synced collection.

Only the robots noindex remains: add it to the shared layout (`Pages/Shared/_Layout.cshtml`) so every page carries it until the owner decides the site may be indexed, with a test on the served HTML.
