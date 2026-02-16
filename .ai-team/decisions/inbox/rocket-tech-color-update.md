### 2026-02-16: Tech branding color palette update

**By:** Rocket

**What:** Updated StructureBuilder color system to modernize tech stack palette and apply Docker aqua branding to Container resources. Rust moved from brown to red, Go moved from cyan to light_blue. Added Container type check returning cyan colors. Expanded language support with PHP (magenta), Ruby (pink), and Elixir/Erlang (lime). Enhanced Warehouse buildings with language-colored accent stripes and banners matching Workshop aesthetic.

**Why:** The previous brown for Rust and cyan for Go didn't match their official branding (Rust logo is red, Go gopher is light blue). Freeing up cyan allowed Docker containers to get their iconic aqua whale color. Warehouses (which house Container types) were missing the tech branding visual identity that Workshops and Watchtowers already had — adding stripes and banners creates consistency across all building types. New language colors fill gaps in the tech stack (PHP/Laravel, Ruby/Rails, Elixir/Phoenix are common Aspire integrations).

**Impact:**
- Standard Warehouse: +2 RCON commands (1 fill, 1 banner)
- Grand Warehouse: +6 RCON commands (2 fills, 4 banners)
- Both well within burst mode limits
- Container resources now instantly recognizable with aqua branding
- More comprehensive language coverage for modern polyglot stacks
