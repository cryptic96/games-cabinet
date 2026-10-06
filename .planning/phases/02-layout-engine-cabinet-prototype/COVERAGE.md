# API Coverage

No external API integration: the phase builds a pure layout engine, synthetic sample data and the cabinet page; BGG access comes later.

The only HTTP surface is the site's own undocumented, UI-only layout endpoint (`GET /cabinet/layout`) serving invented collections; it calls no outside service.
