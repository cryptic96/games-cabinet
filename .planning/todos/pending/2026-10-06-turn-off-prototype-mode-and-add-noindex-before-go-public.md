---
created: 2026-10-06T09:48:44.000Z
title: Turn off prototype mode and add noindex before go-public
area: security
severity: major
files:
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Prototype/SampleCatalog.cs
---

## Problem

The committed appsettings.json turns the prototype on (Prototype:Enabled true), so the deployed site serves the invented sample collections and the sample switcher, and the pages carry no noindex. That is harmless while the route is limited to the home network and VPN, but it must not reach the public internet. Raised by the layout-prototype code review (info item on the prototype default) and the security audit's open items.

## Solution

Part of the Phase 8 go-public checklist: set the prototype off in the committed defaults (or remove the prototype entirely per the prototype removal list in the release summary of the layout prototype), add a robots noindex until the owner decides the site may be indexed, and add a test that the public page shows neither sample links nor the prototype status line when the setting is off.
