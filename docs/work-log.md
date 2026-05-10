# Program Hider Work Log

## 2026-05-10

### Planned Bundles

1. Design doc, work log, and roadmap baseline
2. Rich window rules and rule-capture tooling
3. Restore UX and placement correctness
4. Security/startup/reliability polish
5. Packaging/changelog polish

### Progress Entries

- Started `v0.1.0` planning pass and created the design/work-log artifacts.
- Landed the rich rule engine:
  - migrated settings from process-only rules to structured window rules
  - added rule match fields for process, title substring, and class name
  - added per-rule behaviors for auto-hide, PIN-gated restore, and quiet mode
  - added active-window inspection and one-click rule creation
  - added rule import/export in the settings UI
